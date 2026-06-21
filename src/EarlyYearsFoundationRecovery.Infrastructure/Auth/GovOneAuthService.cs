using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EarlyYearsFoundationRecovery.Infrastructure.Auth;

public sealed class GovOneAuthService(
    IHttpClientFactory httpClientFactory,
    IOptions<GovOneOptions> options,
    IMemoryCache memoryCache,
    ILogger<GovOneAuthService> logger) : IGovOneAuthService
{
    private readonly GovOneOptions _options = options.Value;
    private SigningCredentials? _clientAssertionCredentials;

    public string BuildAuthorizeUrl(string state, string nonce)
    {
        var query = new Dictionary<string, string?>
        {
            ["redirect_uri"] = _options.CallbackUrl,
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = "email openid",
            ["nonce"] = nonce,
            ["state"] = state,
            ["prompt"] = "login",
        };

        return QueryHelpersAppend(_options.AuthorizeEndpoint, query);
    }

    public string BuildLogoutUrl(string idTokenHint, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["post_logout_redirect_uri"] = _options.SignOutUrl,
            ["id_token_hint"] = idTokenHint,
            ["state"] = state,
        };

        return QueryHelpersAppend(_options.LogoutEndpoint, query);
    }

    public async Task<GovOneTokenResponse?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(GovOneAuthService));
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.CallbackUrl,
                ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
                ["client_assertion"] = CreateClientAssertion(),
            }),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("GovOne token exchange failed: {StatusCode}", response.StatusCode);
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var tokenError))
        {
            logger.LogError("GovOne token exchange returned error: {Error}", tokenError.GetString());
            return null;
        }

        var accessToken = root.GetProperty("access_token").GetString();
        var idToken = root.GetProperty("id_token").GetString();
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(idToken))
        {
            return null;
        }

        return new GovOneTokenResponse(accessToken, idToken);
    }

    public async Task<GovOneUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient(nameof(GovOneAuthService));
        using var request = new HttpRequestMessage(HttpMethod.Get, _options.UserInfoEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("GovOne userinfo failed: {StatusCode}", response.StatusCode);
            return null;
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (root.TryGetProperty("error", out var userInfoError))
        {
            logger.LogError("GovOne userinfo returned error: {Error}", userInfoError.GetString());
            return null;
        }

        var sub = root.GetProperty("sub").GetString();
        var email = root.GetProperty("email").GetString();
        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        return new GovOneUserInfo(sub, email);
    }

    public async Task<GovOneIdToken?> ValidateIdTokenAsync(
        string idToken,
        string expectedNonce,
        CancellationToken cancellationToken = default)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(idToken);
        var kid = jwt.Header.Kid;
        if (string.IsNullOrWhiteSpace(kid))
        {
            logger.LogError("GovOne id token missing kid.");
            return null;
        }

        var keys = await GetSigningKeysAsync(cancellationToken);
        var key = keys.FirstOrDefault(x => x.KeyId == kid);
        if (key is null)
        {
            memoryCache.Remove(JwksCacheKey);
            keys = await GetSigningKeysAsync(cancellationToken);
            key = keys.FirstOrDefault(x => x.KeyId == kid);
        }

        if (key is null)
        {
            logger.LogError("GovOne id token kid {Kid} not found in JWKS.", kid);
            return null;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"{_options.BaseUri.TrimEnd('/')}/",
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ClockSkew = TimeSpan.FromMinutes(2),
        };

        try
        {
            handler.ValidateToken(idToken, parameters, out var validatedToken);
            var token = (JwtSecurityToken)validatedToken;
            var nonce = token.Claims.FirstOrDefault(x => x.Type == "nonce")?.Value;
            if (!string.Equals(nonce, expectedNonce, StringComparison.Ordinal))
            {
                logger.LogError("GovOne id token nonce mismatch.");
                return null;
            }

            var sub = token.Claims.FirstOrDefault(x => x.Type == "sub")?.Value;
            if (string.IsNullOrWhiteSpace(sub))
            {
                return null;
            }

            return new GovOneIdToken(sub, nonce!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GovOne id token validation failed.");
            return null;
        }
    }

    private const string JwksCacheKey = "gov-one-jwks";

    private async Task<IReadOnlyList<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken)
    {
        return await memoryCache.GetOrCreateAsync(JwksCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            var jwksUri = await GetJwksUriAsync(cancellationToken);
            var client = httpClientFactory.CreateClient(nameof(GovOneAuthService));
            var json = await client.GetStringAsync(jwksUri, cancellationToken);
            var jwks = new JsonWebKeySet(json);
            return jwks.Keys.Cast<SecurityKey>().ToList();
        }) ?? [];
    }

    private async Task<string> GetJwksUriAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(GovOneAuthService));
        var json = await client.GetStringAsync(_options.OpenIdConfigurationEndpoint, cancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("jwks_uri").GetString()
            ?? $"{_options.BaseUri.TrimEnd('/')}/.well-known/jwks.json";
    }

    private string CreateClientAssertion()
    {
        var now = DateTimeOffset.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Audience = _options.TokenEndpoint,
            Issuer = _options.ClientId,
            Subject = new System.Security.Claims.ClaimsIdentity([new("sub", _options.ClientId)]),
            Expires = now.AddMinutes(5).UtcDateTime,
            IssuedAt = now.UtcDateTime,
            Claims = new Dictionary<string, object> { ["jti"] = Guid.NewGuid().ToString() },
            SigningCredentials = GetClientAssertionCredentials(),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.CreateToken(descriptor) is JwtSecurityToken token
            ? handler.WriteToken(token)
            : throw new InvalidOperationException("Failed to create client assertion.");
    }

    private SigningCredentials GetClientAssertionCredentials()
    {
        if (_clientAssertionCredentials is not null)
        {
            return _clientAssertionCredentials;
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(LoadPrivateKeyPem());
        var key = new RsaSecurityKey(rsa.ExportParameters(includePrivateParameters: true));
        _clientAssertionCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        return _clientAssertionCredentials;
    }

    private string LoadPrivateKeyPem()
    {
        if (!string.IsNullOrWhiteSpace(_options.PrivateKey))
        {
            return _options.PrivateKey.Replace("\\n", "\n", StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(_options.PrivateKeyPath) && File.Exists(_options.PrivateKeyPath))
        {
            return File.ReadAllText(_options.PrivateKeyPath);
        }

        throw new InvalidOperationException("GovOne private key is not configured.");
    }

    private static string QueryHelpersAppend(string baseUrl, Dictionary<string, string?> query)
    {
        var builder = new StringBuilder(baseUrl);
        builder.Append('?');
        builder.Append(string.Join('&', query
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}")));
        return builder.ToString();
    }
}
