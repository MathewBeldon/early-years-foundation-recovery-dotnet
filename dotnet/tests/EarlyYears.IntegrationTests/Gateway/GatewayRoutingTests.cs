using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyYears.IntegrationTests.Gateway;

public sealed class GatewayRoutingTests
{
    [Fact]
    public async Task CatchAllPreservesMethodPathQueryHostAndForwardedInformation()
    {
        await using var backend = await StubBackend.StartAsync();
        using var factory = GatewayFactory(backend.Address);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("http://migration.example"),
        });
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri("/settings?source=parity", UriKind.Relative))
        {
            Content = JsonContent.Create(new { selected = true }),
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.9");

        using var response = await client.SendAsync(request);
        var captured = await response.Content.ReadFromJsonAsync<CapturedRequest>();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(captured);
        Assert.Equal("POST", captured.Method);
        Assert.Equal("/settings", captured.Path);
        Assert.Equal("?source=parity", captured.Query);
        Assert.Equal("migration.example", captured.Host);
        Assert.Contains("migration.example", captured.ForwardedHost, StringComparison.Ordinal);
        Assert.Contains("http", captured.ForwardedProto, StringComparison.Ordinal);
        Assert.Contains("203.0.113.9", captured.ForwardedFor, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GatewayHealthIsOwnedByGatewayAndNotProxied()
    {
        await using var backend = await StubBackend.StartAsync();
        using var factory = GatewayFactory(backend.Address);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/gateway-health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, backend.RequestCount);
    }

    private static WebApplicationFactory<MigrationGateway.Program> GatewayFactory(Uri backendAddress) =>
        new WebApplicationFactory<MigrationGateway.Program>()
            .WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ReverseProxy:Clusters:rails:Destinations:primary:Address"] = backendAddress.ToString(),
                });
            }));

    private sealed class StubBackend : IAsyncDisposable
    {
        private readonly WebApplication _application;
        private int _requestCount;

        private StubBackend(WebApplication application, Uri address)
        {
            _application = application;
            Address = address;
        }

        public Uri Address { get; }

        public int RequestCount => _requestCount;

        public static async Task<StubBackend> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
            var application = builder.Build();
            StubBackend? backend = null;
            application.Map("/{**path}", (HttpContext context) =>
            {
                Interlocked.Increment(ref backend!._requestCount);
                context.Response.StatusCode = StatusCodes.Status202Accepted;
                return context.Response.WriteAsJsonAsync(new CapturedRequest(
                    context.Request.Method,
                    context.Request.Path,
                    context.Request.QueryString.Value ?? string.Empty,
                    context.Request.Host.Value ?? string.Empty,
                    context.Request.Headers["X-Forwarded-For"].ToString(),
                    context.Request.Headers["X-Forwarded-Host"].ToString(),
                    context.Request.Headers["X-Forwarded-Proto"].ToString()));
            });

            await application.StartAsync();
            var addresses = application.Services
                .GetRequiredService<IServer>()
                .Features
                .Get<IServerAddressesFeature>()
                ?.Addresses;
            var address = new Uri(addresses?.Single()
                ?? throw new InvalidOperationException("The stub backend did not publish an address."));
            backend = new StubBackend(application, address);
            return backend;
        }

        public async ValueTask DisposeAsync()
        {
            await _application.DisposeAsync();
        }
    }

    private sealed record CapturedRequest(
        string Method,
        string Path,
        string Query,
        string Host,
        string ForwardedFor,
        string ForwardedHost,
        string ForwardedProto);
}
