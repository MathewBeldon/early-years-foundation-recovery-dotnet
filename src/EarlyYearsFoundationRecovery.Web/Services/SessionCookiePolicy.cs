namespace EarlyYearsFoundationRecovery.Web.Services;

public static class SessionCookiePolicy
{
    public static CookieSecurePolicy ForEnvironment(string environmentName) =>
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
        || string.Equals(environmentName, "Parity", StringComparison.OrdinalIgnoreCase)
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
}
