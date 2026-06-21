namespace EarlyYearsFoundationRecovery.Infrastructure.Auth;

public sealed class GovOneOptions
{
    public const string SectionName = "GovOne";

    public string BaseUri { get; set; } = "http://localhost:3333";
    public string ClientId { get; set; } = "HGIOgho9HIRhgoepdIOPFdIUWgewi0jw";
    public string ServiceUrl { get; set; } = "http://localhost:5000";
    public string? PrivateKey { get; set; }
    public string? PrivateKeyPath { get; set; }

    public string CallbackPath => "/users/auth/openid_connect/callback";
    public string SignOutPath => "/users/sign_out";

    public string CallbackUrl => $"{ServiceUrl.TrimEnd('/')}{CallbackPath}";
    public string SignOutUrl => $"{ServiceUrl.TrimEnd('/')}{SignOutPath}";

    public string AuthorizeEndpoint => $"{BaseUri.TrimEnd('/')}/authorize";
    public string TokenEndpoint => $"{BaseUri.TrimEnd('/')}/token";
    public string UserInfoEndpoint => $"{BaseUri.TrimEnd('/')}/userinfo";
    public string LogoutEndpoint => $"{BaseUri.TrimEnd('/')}/logout";
    public string OpenIdConfigurationEndpoint => $"{BaseUri.TrimEnd('/')}/.well-known/openid-configuration";
}
