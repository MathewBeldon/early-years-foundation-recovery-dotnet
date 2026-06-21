namespace EarlyYearsFoundationRecovery.Web.Authentication;

public static class AuthConstants
{
    public const string Scheme = "Cookies";
    public const string UserIdClaim = "user_id";
    public const string EmailClaim = "email";

    public const string GovOneStateSessionKey = "GovOneAuthState";
    public const string GovOneNonceSessionKey = "GovOneAuthNonce";
    public const string GovOneIdTokenSessionKey = "GovOneIdToken";
}
