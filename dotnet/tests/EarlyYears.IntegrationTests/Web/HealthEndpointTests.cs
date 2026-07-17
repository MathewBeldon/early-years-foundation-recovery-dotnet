using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EarlyYears.IntegrationTests.Web;

public sealed class HealthEndpointTests
    : IClassFixture<WebApplicationFactory<EarlyYears.Web.Program>>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(WebApplicationFactory<EarlyYears.Web.Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
    }

    [Fact]
    public async Task LivenessDoesNotRequireDatabaseConnectivity()
    {
        using var response = await _client.GetAsync(new Uri("/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UnmigratedBusinessPathIsNotImplementedByDotNet()
    {
        using var response = await _client.GetAsync(new Uri("/settings/cookie-policy", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
