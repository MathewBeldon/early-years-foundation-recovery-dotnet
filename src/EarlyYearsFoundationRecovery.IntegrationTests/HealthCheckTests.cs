using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly HttpClient _clientWithoutRedirect;

    public HealthCheckTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _clientWithoutRedirect = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task Health_returns_ok_when_database_is_available()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"ok\"", body);
    }

    [Fact]
    public async Task HomePage_returns_success()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AboutTraining_returns_success()
    {
        var response = await _client.GetAsync("/about-training");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("About this training course", body);
        Assert.Contains("Modules", body);
    }

    [Fact]
    public async Task MyAccount_redirects_to_sign_in_when_not_authenticated()
    {
        var response = await _clientWithoutRedirect.GetAsync("/my-account");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/users/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LearningLog_redirects_to_sign_in_when_not_authenticated()
    {
        var response = await _clientWithoutRedirect.GetAsync("/my-account/learning-log");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/users/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Feedback_redirects_to_sign_in_when_not_authenticated()
    {
        var response = await _clientWithoutRedirect.GetAsync("/feedback");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/users/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rails_sign_in_path_returns_success()
    {
        var response = await _client.GetAsync("/users/sign-in");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Rails_training_paths_redirect_to_sign_in_when_not_authenticated()
    {
        var contentPage = await _clientWithoutRedirect.GetAsync("/modules/module-1/content-pages/what-to-expect");
        var questionnaire = await _clientWithoutRedirect.GetAsync("/modules/module-1/questionnaires/check-understanding");
        var assessmentResult = await _clientWithoutRedirect.GetAsync("/modules/module-1/assessment-result/assessment-results");

        Assert.Equal(HttpStatusCode.Redirect, contentPage.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, questionnaire.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, assessmentResult.StatusCode);
        Assert.Contains("/users/sign-in", contentPage.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/users/sign-in", questionnaire.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/users/sign-in", assessmentResult.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rails_about_experts_path_redirects_to_about_training()
    {
        var response = await _clientWithoutRedirect.GetAsync("/about/the-experts");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/about-training", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rails_audit_path_returns_success()
    {
        var response = await _client.GetAsync("/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AccessibilityStatement_returns_success()
    {
        var response = await _client.GetAsync("/accessibility-statement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accessibility statement", body);
    }

    [Fact]
    public async Task TermsAndConditions_returns_success()
    {
        var response = await _client.GetAsync("/terms-and-conditions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Terms and conditions", body);
    }

    [Fact]
    public async Task CookiePolicy_returns_success()
    {
        var response = await _client.GetAsync("/settings/cookie-policy");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Cookie policy", body);
        Assert.Contains("Save cookie settings", body);
    }

    [Fact]
    public async Task Sitemap_returns_success()
    {
        var response = await _client.GetAsync("/sitemap");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Sitemap", body);
    }

    [Fact]
    public async Task WhatsNew_redirects_when_not_authenticated()
    {
        var response = await _clientWithoutRedirect.GetAsync("/whats-new");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task UnknownStaticPage_returns_not_found()
    {
        var response = await _client.GetAsync("/foo");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/404", HttpStatusCode.NotFound)]
    [InlineData("/500", HttpStatusCode.InternalServerError)]
    [InlineData("/503", HttpStatusCode.ServiceUnavailable)]
    public async Task Rails_error_paths_return_expected_status(string path, HttpStatusCode expectedStatus)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task Rails_release_webhook_path_is_routed()
    {
        var response = await _client.PostAsync("/release", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("contentful not configured", body);
    }

    [Fact]
    public async Task CloseAccount_edit_reason_redirects_when_not_authenticated()
    {
        var response = await _clientWithoutRedirect.GetAsync("/my-account/close/edit-reason");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task CloseAccount_show_returns_success_without_authentication()
    {
        var response = await _client.GetAsync("/my-account/close");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Account closed", body);
    }

    [Fact]
    public async Task HomePage_shows_cookie_banner_when_preference_not_set()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accept analytics cookies", body);
        Assert.Contains("Reject analytics cookies", body);
    }

    [Fact]
    public async Task HomePage_hides_cookie_banner_when_preference_is_set()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Cookie", "track_analytics_v2=false");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Accept analytics cookies", body);
    }

    [Fact]
    public async Task Settings_save_sets_analytics_cookie_and_redirects()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var page = await client.GetAsync("/");
        var html = await page.Content.ReadAsStringAsync();
        var token = ExtractAntiForgeryToken(html);

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["TrackAnalytics"] = "true",
            ["RequestPath"] = "/",
            ["__RequestVerificationToken"] = token,
        });

        var response = await client.PostAsync("/settings", form);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
        Assert.Contains("track_analytics_v2=true", response.Headers.GetValues("Set-Cookie").First());
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("Anti-forgery token not found.");
        }

        var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
