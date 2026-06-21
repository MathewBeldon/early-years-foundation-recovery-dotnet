namespace EarlyYearsFoundationRecovery.Application.Registration;

public static class PostSignInRedirect
{
    public const string MyModulesPath = "/my-modules";

    public static string ResolveRegisteredUserDestination() => MyModulesPath;

    public static string ResolveAfterRegistrationComplete() => MyModulesPath;
}
