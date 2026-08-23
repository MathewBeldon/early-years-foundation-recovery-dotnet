using System.Net;
using EarlyYearsFoundationRecovery.Web.Middleware;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class ContentSecurityPolicyTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string HeaderName = "Content-Security-Policy";
    private readonly CustomWebApplicationFactory _factory;

    public ContentSecurityPolicyTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/", HttpStatusCode.OK)]
    [InlineData("/my-account", HttpStatusCode.Redirect)]
    [InlineData("/route-that-does-not-exist", HttpStatusCode.NotFound)]
    [InlineData("/css/site.css", HttpStatusCode.OK)]
    public async Task Testing_environment_enforces_the_same_policy_on_every_response(
        string path,
        HttpStatusCode expectedStatus)
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync(path);

        Assert.Equal(expectedStatus, response.StatusCode);
        AssertCanonicalPolicy(response);
    }

    [Fact]
    public async Task Development_environment_enforces_the_canonical_policy()
    {
        using var host = await Host.CreateDefaultBuilder()
            .UseEnvironment("Development")
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .Configure(app =>
                {
                    app.UseContentSecurityPolicy();
                    app.Use(_ => context => context.Response.WriteAsync("development"));
                }))
            .StartAsync();

        using var response = await host.GetTestClient().GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertCanonicalPolicy(response);
    }

    private static void AssertCanonicalPolicy(HttpResponseMessage response)
    {
        Assert.False(response.Headers.Contains("Content-Security-Policy-Report-Only"));
        var values = response.Headers.GetValues(HeaderName).ToArray();
        Assert.Single(values);
        Assert.Equal(ContentSecurityPolicyMiddleware.Policy, values[0]);

        var directives = ParseDirectives(values[0]);

        Assert.Equal(["'none'"], directives["default-src"]);
        Assert.Equal(["'self'"], directives["base-uri"]);
        Assert.Equal(["'self'"], directives["connect-src"]);
        Assert.Equal(["'self'", "https://fonts.gstatic.com", "data:"], directives["font-src"]);
        Assert.Equal(["'self'"], directives["form-action"]);
        Assert.Equal(["'self'"], directives["frame-ancestors"]);
        Assert.Equal(["https://www.youtube.com", "https://player.vimeo.com"], directives["frame-src"]);
        Assert.Equal(["'self'", "https://images.ctfassets.net", "data:"], directives["img-src"]);
        Assert.Equal(["'self'", "https://player.vimeo.com", "https://i.vimeocdn.com"], directives["media-src"]);
        Assert.Equal(["'none'"], directives["object-src"]);
        Assert.Equal(["'self'"], directives["script-src"]);
        Assert.Equal(["'self'", "'unsafe-inline'", "https://fonts.googleapis.com"], directives["style-src"]);
        Assert.Empty(directives["upgrade-insecure-requests"]);
        Assert.Empty(directives["block-all-mixed-content"]);

        Assert.DoesNotContain("'unsafe-inline'", directives["script-src"]);
        Assert.DoesNotContain("'unsafe-eval'", directives["script-src"]);
        Assert.DoesNotContain(directives.SelectMany(directive => directive.Value), source => source.Contains('*'));
    }

    private static IReadOnlyDictionary<string, string[]> ParseDirectives(string policy) =>
        policy.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directive => directive.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToDictionary(parts => parts[0], parts => parts[1..], StringComparer.Ordinal);
}
