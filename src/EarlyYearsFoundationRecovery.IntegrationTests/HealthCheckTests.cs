using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
    public async Task Feedback_intro_is_available_when_not_authenticated()
    {
        var response = await _clientWithoutRedirect.GetAsync("/feedback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Give feedback", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
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
    public async Task Rails_about_experts_path_renders_the_experts_page()
    {
        var response = await _clientWithoutRedirect.GetAsync("/about/the-experts");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1", body, StringComparison.Ordinal);
        Assert.Contains("The experts", body, StringComparison.Ordinal);
        Assert.Contains("This training course has been created by early years experts.", body, StringComparison.Ordinal);
        Assert.Contains("working as early years practitioners", body, StringComparison.Ordinal);
        Assert.Contains("href=\"/about/the-experts\"", body, StringComparison.Ordinal);
        Assert.Contains("Create an account or sign in", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rails_audit_path_returns_success()
    {
        // Mirrors ac546721 app/controllers/home_controller.rb:2,22-23 and concerns/auditing.rb:11-16.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/audit");
        request.Headers.Add("BOT", "test-audit-token");
        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BOT ACCESS GRANTED", await response.Content.ReadAsStringAsync());
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
        // Mirrors ac546721 app/controllers/webhook_controller.rb:9-22.
        var response = await _client.PostAsync("/release", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("invalid secure header", body);
    }

    [Fact]
    public async Task Rails_release_webhook_persists_authorized_payload()
    {
        // Mirrors ac546721 app/controllers/webhook_controller.rb:9-22.
        var request = new HttpRequestMessage(HttpMethod.Post, "/release")
        {
            Content = new StringContent("{\"sys\":{\"id\":\"release-1\",\"completedAt\":\"2026-07-27T12:00:00Z\"}}", System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("BOT", "test-contentful-token");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("content release received", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Notify_webhook_rejects_missing_secure_header()
    {
        // Mirrors ac546721 app/controllers/notify_controller.rb:21-30.
        var response = await _client.PostAsync("/notify", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("invalid secure header", await response.Content.ReadAsStringAsync());
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
        IntegrationTestHost.Configure(builder);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Audit:BotToken"] = "test-audit-token",
                ["Contentful:WebhookSecret"] = "test-contentful-token",
                ["Notify:CallbackToken"] = "test-notify-token",
            }));
    }
}
