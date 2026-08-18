using EarlyYearsFoundationRecovery.Infrastructure.Auth;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class GovOneOptionsTests
{
    [Fact]
    public void Browser_facing_endpoints_can_differ_from_back_channel_endpoints()
    {
        var options = new GovOneOptions
        {
            BaseUri = "http://gov-one-login-simulator:3000/",
            BrowserBaseUri = "http://localhost:3333/",
        };

        Assert.Equal("http://localhost:3333/authorize", options.AuthorizeEndpoint);
        Assert.Equal("http://localhost:3333/logout", options.LogoutEndpoint);
        Assert.Equal("http://gov-one-login-simulator:3000/token", options.TokenEndpoint);
        Assert.Equal("http://gov-one-login-simulator:3000/userinfo", options.UserInfoEndpoint);
        Assert.Equal(
            "http://gov-one-login-simulator:3000/.well-known/openid-configuration",
            options.OpenIdConfigurationEndpoint);
    }

    [Fact]
    public void Browser_facing_endpoints_default_to_the_back_channel_base_uri()
    {
        var options = new GovOneOptions { BaseUri = "https://identity.example" };

        Assert.Equal("https://identity.example/authorize", options.AuthorizeEndpoint);
        Assert.Equal("https://identity.example/logout", options.LogoutEndpoint);
    }
}
