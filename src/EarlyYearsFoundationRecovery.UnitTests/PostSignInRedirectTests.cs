using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class PostSignInRedirectTests
{
    [Fact]
    public void ResolveRegisteredUserDestination_returns_whats_new_and_clears_flag()
    {
        // Rails v1.5.0 ac546721 app/controllers/users/omniauth_callbacks_controller.rb:99-104 clears the flag before /whats-new.
        var user = new User { RegistrationComplete = true, DisplayWhatsNew = true, TrainingEmails = null };

        var destination = PostSignInRedirect.ResolveRegisteredUserDestination(user);

        Assert.Equal(PostSignInRedirect.WhatsNewPath, destination);
        Assert.False(user.DisplayWhatsNew);
    }

    [Fact]
    public void ResolveRegisteredUserDestination_returns_email_preferences_when_preference_is_incomplete()
    {
        // Rails v1.5.0 ac546721 app/controllers/users/omniauth_callbacks_controller.rb:105-106 and app/models/user.rb:438-440 define incomplete as training_emails nil.
        var user = new User { RegistrationComplete = true, DisplayWhatsNew = false, TrainingEmails = null };

        var destination = PostSignInRedirect.ResolveRegisteredUserDestination(user);

        Assert.Equal(PostSignInRedirect.EmailPreferencesPath, destination);
    }

    [Fact]
    public void ResolveRegisteredUserDestination_returns_my_modules_when_interruptions_are_complete()
    {
        // Rails v1.5.0 ac546721 app/controllers/users/omniauth_callbacks_controller.rb:105-108 defaults registered users to /my-modules.
        var user = new User { RegistrationComplete = true, DisplayWhatsNew = false, TrainingEmails = false };

        var destination = PostSignInRedirect.ResolveRegisteredUserDestination(user);

        Assert.Equal(PostSignInRedirect.MyModulesPath, destination);
    }

    [Fact]
    public void ResolveIncompleteUserDestination_returns_terms_and_conditions_edit()
    {
        // Rails v1.5.0 ac546721 app/controllers/users/omniauth_callbacks_controller.rb:110-111 always sends incomplete users to terms edit.
        Assert.Equal(PostSignInRedirect.TermsAndConditionsPath, PostSignInRedirect.ResolveIncompleteUserDestination());
    }

    [Fact]
    public void ResolveAfterRegistrationComplete_returns_my_modules()
    {
        Assert.Equal(PostSignInRedirect.MyModulesPath, PostSignInRedirect.ResolveAfterRegistrationComplete());
    }
}
