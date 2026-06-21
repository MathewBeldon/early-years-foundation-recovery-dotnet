using EarlyYearsFoundationRecovery.Application.Registration;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class PostSignInRedirectTests
{
    [Fact]
    public void ResolveRegisteredUserDestination_returns_my_modules()
    {
        Assert.Equal(PostSignInRedirect.MyModulesPath, PostSignInRedirect.ResolveRegisteredUserDestination());
    }

    [Fact]
    public void ResolveAfterRegistrationComplete_returns_my_modules()
    {
        Assert.Equal(PostSignInRedirect.MyModulesPath, PostSignInRedirect.ResolveAfterRegistrationComplete());
    }
}
