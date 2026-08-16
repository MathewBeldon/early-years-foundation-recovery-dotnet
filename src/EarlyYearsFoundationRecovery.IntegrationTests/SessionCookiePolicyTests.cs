using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Http;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class SessionCookiePolicyTests
{
    [Theory]
    [InlineData("Testing", CookieSecurePolicy.SameAsRequest)]
    [InlineData("Parity", CookieSecurePolicy.SameAsRequest)]
    [InlineData("Development", CookieSecurePolicy.Always)]
    [InlineData("Production", CookieSecurePolicy.Always)]
    public void Http_session_override_is_limited_to_testing_and_parity(
        string environment,
        CookieSecurePolicy expected) =>
        Assert.Equal(expected, SessionCookiePolicy.ForEnvironment(environment));
}
