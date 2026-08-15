using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Registration;

public static class PostSignInRedirect
{
    public const string MyModulesPath = "/my-modules";
    public const string WhatsNewPath = "/whats-new";
    public const string EmailPreferencesPath = "/email-preferences";
    public const string TermsAndConditionsPath = "/registration/terms-and-conditions/edit";

    public static string ResolveRegisteredUserDestination(User user)
    {
        if (user.DisplayWhatsNew)
        {
            user.DisplayWhatsNew = false;
            return WhatsNewPath;
        }

        return user.TrainingEmails is null ? EmailPreferencesPath : MyModulesPath;
    }

    public static string ResolveIncompleteUserDestination() => TermsAndConditionsPath;

    public static string ResolveAfterRegistrationComplete() => MyModulesPath;
}
