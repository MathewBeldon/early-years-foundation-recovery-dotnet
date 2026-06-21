namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IGovOneAuthService
{
    string BuildAuthorizeUrl(string state, string nonce);
    string BuildLogoutUrl(string idTokenHint, string state);
    Task<GovOneTokenResponse?> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<GovOneUserInfo?> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<GovOneIdToken?> ValidateIdTokenAsync(string idToken, string expectedNonce, CancellationToken cancellationToken = default);
}

public sealed record GovOneTokenResponse(string AccessToken, string IdToken);
public sealed record GovOneUserInfo(string Sub, string Email);
public sealed record GovOneIdToken(string Sub, string Nonce);
