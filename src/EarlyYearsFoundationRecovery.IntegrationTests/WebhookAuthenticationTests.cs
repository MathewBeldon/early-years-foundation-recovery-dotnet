using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using EarlyYearsFoundationRecovery.Web.Authentication;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

[Collection("Webhook credential environment")]
public sealed class WebhookAuthenticationTests
{
    private const string AuditToken = "audit-secret-123";
    private const string ContentfulToken = "contentful-secret-123";
    private const string NotifyToken = "notify-secret-123";

    [Fact]
    public async Task Notify_correct_credential_is_accepted()
    {
        // Mirrors ac546721 app/controllers/notify_controller.rb:21-30.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendNotifyAsync(client, "Bearer " + NotifyToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("notify-secret")]
    [InlineData("secret-123")]
    [InlineData("secret")]
    [InlineData(AuditToken)]
    [InlineData(ContentfulToken)]
    public async Task Notify_non_exact_or_other_endpoint_credential_is_rejected(string token)
    {
        // Mirrors ac546721 app/controllers/notify_controller.rb:21-30.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendNotifyAsync(client, "Bearer " + token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Notify_extracts_everything_after_the_first_space_without_validating_the_scheme()
    {
        // Mirrors ac546721 app/controllers/notify_controller.rb:29-30.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendNotifyAsync(client, "Anything " + NotifyToken);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Notify_missing_or_blank_configured_credential_fails_closed(string? configuredToken)
    {
        // Mirrors ac546721 app/controllers/notify_controller.rb:21-26.
        await using var factory = CreateFactory(notifyToken: configuredToken);
        using var client = factory.CreateClient();

        var response = await SendNotifyAsync(client, "Bearer public-or-empty-value");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Contentful_correct_credential_is_accepted()
    {
        // Mirrors ac546721 app/controllers/webhook_controller.rb:17-22.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendContentfulAsync(client, ContentfulToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("contentful-secret")]
    [InlineData("secret-123")]
    [InlineData("secret")]
    [InlineData(AuditToken)]
    [InlineData(NotifyToken)]
    public async Task Contentful_non_exact_or_other_endpoint_credential_is_rejected(string token)
    {
        // Mirrors ac546721 app/controllers/webhook_controller.rb:17-22.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendContentfulAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Contentful_missing_or_blank_configured_credential_fails_closed(string? configuredToken)
    {
        // Mirrors ac546721 app/controllers/webhook_controller.rb:17-22.
        await using var factory = CreateFactory(contentfulToken: configuredToken);
        using var client = factory.CreateClient();

        var response = await SendContentfulAsync(client, "public-or-empty-value");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Contentful_non_upstream_secret_header_is_rejected()
    {
        // Mirrors ac546721 app/controllers/webhook_controller.rb:17-22.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/change")
        {
            Content = ContentfulPayload(),
        };
        request.Headers.Add("X-Contentful-Webhook-Secret", ContentfulToken);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Contentful_configured_credential_is_compared_without_trimming()
    {
        // Mirrors ac546721 app/controllers/webhook_controller.rb:17-22.
        await using var factory = CreateFactory(contentfulToken: ContentfulToken + " ");
        using var client = factory.CreateClient();

        var exact = await SendContentfulAsync(client, ContentfulToken + " ");
        var trimmed = await SendContentfulAsync(client, ContentfulToken);

        Assert.Equal(HttpStatusCode.OK, exact.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, trimmed.StatusCode);
    }

    [Fact]
    public async Task Audit_correct_credential_returns_Rails_plain_text()
    {
        // Mirrors ac546721 app/controllers/home_controller.rb:2,22-23 and concerns/auditing.rb:11-16.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendAuditAsync(client, AuditToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("BOT ACCESS GRANTED", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("audit-secret")]
    [InlineData("secret-123")]
    [InlineData("secret")]
    [InlineData(ContentfulToken)]
    [InlineData(NotifyToken)]
    public async Task Audit_non_exact_or_other_endpoint_credential_is_rejected(string token)
    {
        // Mirrors ac546721 app/controllers/concerns/auditing.rb:11-16.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendAuditAsync(client, token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Audit_missing_or_blank_configured_credential_fails_closed(string? configuredToken)
    {
        // Mirrors ac546721 app/controllers/concerns/auditing.rb:11-16.
        await using var factory = CreateFactory(auditToken: configuredToken);
        using var client = factory.CreateClient();

        var response = await SendAuditAsync(client, "public-or-empty-value");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("notify")]
    [InlineData("contentful")]
    [InlineData("audit")]
    public async Task Failed_authentication_returns_429_after_twenty_failures(string endpoint)
    {
        // Mirrors ac546721 app/controllers/concerns/bot_auth_protection.rb:4-5,15-22,26-39.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var failure = await SendAsync(client, endpoint, "wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var limited = await SendAsync(client, endpoint, "wrong");

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Contains("rate limited", await limited.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("notify")]
    [InlineData("contentful")]
    [InlineData("audit")]
    public async Task Successful_authentication_does_not_count_and_resets_failures(string endpoint)
    {
        // Mirrors ac546721 app/controllers/concerns/bot_auth_protection.rb:9-13,42-43.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 25; attempt++)
        {
            var success = await SendAsync(client, endpoint, CorrectToken(endpoint));
            Assert.NotEqual(HttpStatusCode.Unauthorized, success.StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, success.StatusCode);
        }
        for (var attempt = 0; attempt < 19; attempt++)
        {
            var failure = await SendAsync(client, endpoint, "wrong");
            Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode);
        }

        var reset = await SendAsync(client, endpoint, CorrectToken(endpoint));
        var failureAfterReset = await SendAsync(client, endpoint, "wrong");

        Assert.NotEqual(HttpStatusCode.TooManyRequests, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, failureAfterReset.StatusCode);
    }

    [Fact]
    public void Failed_authentication_counter_is_scoped_by_endpoint_and_client_ip()
    {
        // Mirrors ac546721 app/controllers/concerns/bot_auth_protection.rb:46-51.
        var tracker = new BotAuthenticationFailureTracker(TimeProvider.System);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.False(tracker.RegisterFailure("audit", "192.0.2.1"));
        }

        Assert.True(tracker.RegisterFailure("audit", "192.0.2.1"));
        Assert.False(tracker.RegisterFailure("notify-webhook", "192.0.2.1"));
        Assert.False(tracker.RegisterFailure("audit", "192.0.2.2"));
    }

    [Fact]
    public void Failed_authentication_counter_expires_after_five_minutes()
    {
        // Mirrors ac546721 app/controllers/concerns/bot_auth_protection.rb:4,30-35.
        var timeProvider = new ManualTimeProvider();
        var tracker = new BotAuthenticationFailureTracker(timeProvider);
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.False(tracker.RegisterFailure("audit", "192.0.2.1"));
        }
        Assert.True(tracker.RegisterFailure("audit", "192.0.2.1"));

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        Assert.False(tracker.RegisterFailure("audit", "192.0.2.1"));
    }

    [Fact]
    public async Task Rails_credential_environment_fallbacks_preserve_precedence_and_ownership()
    {
        // Mirrors ac546721 config/application.rb:57-61.
        var names = new[]
        {
            "BOT_TOKEN",
            "AUDIT_BOT_TOKEN",
            "CONTENTFUL_WEBHOOK_TOKEN",
            "NOTIFY_WEBHOOK_TOKEN",
            "Audit__BotToken",
            "Contentful__WebhookSecret",
            "Notify__CallbackToken",
        };
        var original = names.ToDictionary(name => name, Environment.GetEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable("BOT_TOKEN", "shared-fallback");
            Environment.SetEnvironmentVariable("AUDIT_BOT_TOKEN", "rails-audit");
            Environment.SetEnvironmentVariable("CONTENTFUL_WEBHOOK_TOKEN", "rails-contentful");
            Environment.SetEnvironmentVariable("NOTIFY_WEBHOOK_TOKEN", "rails-notify");
            Environment.SetEnvironmentVariable("Audit__BotToken", null);
            Environment.SetEnvironmentVariable("Contentful__WebhookSecret", null);
            Environment.SetEnvironmentVariable("Notify__CallbackToken", "native-notify");

            await using (var factory = CreateUnconfiguredFactory())
            using (var client = factory.CreateClient())
            {
                Assert.Equal(HttpStatusCode.OK, (await SendAuditAsync(client, "rails-audit")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await SendContentfulAsync(client, "rails-contentful")).StatusCode);
                Assert.Equal(HttpStatusCode.NotModified, (await SendNotifyAsync(client, "Bearer native-notify")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await SendNotifyAsync(client, "Bearer rails-notify")).StatusCode);
                Assert.Equal(HttpStatusCode.Unauthorized, (await SendAuditAsync(client, "rails-contentful")).StatusCode);
            }

            Environment.SetEnvironmentVariable("AUDIT_BOT_TOKEN", null);
            Environment.SetEnvironmentVariable("CONTENTFUL_WEBHOOK_TOKEN", null);
            Environment.SetEnvironmentVariable("NOTIFY_WEBHOOK_TOKEN", null);
            Environment.SetEnvironmentVariable("Notify__CallbackToken", null);

            await using (var factory = CreateUnconfiguredFactory())
            using (var client = factory.CreateClient())
            {
                Assert.Equal(HttpStatusCode.OK, (await SendAuditAsync(client, "shared-fallback")).StatusCode);
                Assert.Equal(HttpStatusCode.OK, (await SendContentfulAsync(client, "shared-fallback")).StatusCode);
                Assert.Equal(
                    HttpStatusCode.NotModified,
                    (await SendNotifyAsync(client, "Bearer shared-fallback")).StatusCode);
            }
        }
        finally
        {
            foreach (var pair in original)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string? auditToken = AuditToken,
        string? contentfulToken = ContentfulToken,
        string? notifyToken = NotifyToken)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Audit:BotToken"] = auditToken,
                    ["Contentful:WebhookSecret"] = contentfulToken,
                    ["Notify:CallbackToken"] = notifyToken,
                }));
        });
    }

    private static WebApplicationFactory<Program> CreateUnconfiguredFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Testing"));

    private static Task<HttpResponseMessage> SendNotifyAsync(HttpClient client, string authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/notify")
        {
            Content = new StringContent("{}"),
        };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendContentfulAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/change")
        {
            Content = ContentfulPayload(),
        };
        request.Headers.Add("BOT", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendAuditAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/audit");
        request.Headers.Add("BOT", token);
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string endpoint, string token) =>
        endpoint switch
        {
            "notify" => SendNotifyAsync(client, "Bearer " + token),
            "contentful" => SendContentfulAsync(client, token),
            "audit" => SendAuditAsync(client, token),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
        };

    private static string CorrectToken(string endpoint) =>
        endpoint switch
        {
            "notify" => NotifyToken,
            "contentful" => ContentfulToken,
            "audit" => AuditToken,
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint), endpoint, null),
        };

    private static StringContent ContentfulPayload() =>
        new("""{"sys":{"id":"change","updatedAt":"2026-08-15T12:00:00Z"}}""");

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}

[CollectionDefinition("Webhook credential environment", DisableParallelization = true)]
public sealed class WebhookCredentialEnvironmentCollection;
