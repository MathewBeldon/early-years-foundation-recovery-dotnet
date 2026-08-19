using System.Net;
using System.Net.Http.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class ContentfulWebhookBoundaryTests
{
    private const string ContentfulToken = "contentful-secret-123";
    private const string InvalidPayloadTitle = "Invalid Contentful webhook payload";

    public static TheoryData<string, string, string> InvalidAuthenticatedPayloads => new()
    {
        { "/release", "", "Request body must contain a valid JSON object." },
        { "/release", "{", "Request body must contain a valid JSON object." },
        { "/release", "[]", "Request body must contain a valid JSON object." },
        { "/release", "{}", "Payload must contain a sys object." },
        { "/release", "{\"sys\":[]}", "Payload must contain a sys object." },
        { "/release", "{\"sys\":{}}", "Payload must contain a non-blank string sys.id." },
        { "/release", "{\"sys\":{\"id\":\"   \"}}", "Payload must contain a non-blank string sys.id." },
        { "/release", "{\"sys\":{\"id\":1}}", "Payload must contain a non-blank string sys.id." },
        { "/release", "{\"sys\":{\"id\":\"release-1\"}}", "Payload must contain a valid sys.completedAt timestamp." },
        { "/release", "{\"sys\":{\"id\":\"release-1\",\"completedAt\":\"not-a-date\"}}", "Payload must contain a valid sys.completedAt timestamp." },
        { "/change", "{\"sys\":{\"id\":\"change-1\"}}", "Payload must contain a valid sys.updatedAt timestamp." },
        { "/change", "{\"sys\":{\"id\":\"change-1\",\"updatedAt\":false}}", "Payload must contain a valid sys.updatedAt timestamp." },
    };

    [Theory]
    [MemberData(nameof(InvalidAuthenticatedPayloads))]
    public async Task Invalid_authenticated_payload_returns_ProblemDetails_without_side_effects(
        string path,
        string payload,
        string expectedDetail)
    {
        var cache = new TrackingContentfulContentCache();
        var jobs = new TrackingBackgroundJobService();
        await using var factory = CreateFactory(cache, jobs);
        using var client = factory.CreateClient();
        var releasesBefore = await CountReleasesAsync(factory);

        var response = await SendAsync(client, path, payload, ContentfulToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(InvalidPayloadTitle, problem.Title);
        Assert.Equal(expectedDetail, problem.Detail);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(0, cache.InvalidationCount);
        Assert.Equal(releasesBefore, await CountReleasesAsync(factory));
        Assert.Equal(0, jobs.EnqueueCount);
    }

    [Fact]
    public async Task Malformed_unauthenticated_payload_stays_unauthorized_without_side_effects()
    {
        var cache = new TrackingContentfulContentCache();
        var jobs = new TrackingBackgroundJobService();
        await using var factory = CreateFactory(cache, jobs);
        using var client = factory.CreateClient();
        var releasesBefore = await CountReleasesAsync(factory);

        var response = await SendAsync(client, "/release", "{", "wrong-token");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, cache.InvalidationCount);
        Assert.Equal(releasesBefore, await CountReleasesAsync(factory));
        Assert.Equal(0, jobs.EnqueueCount);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        TrackingContentfulContentCache cache,
        TrackingBackgroundJobService jobs)
    {
        var databaseName = Guid.NewGuid().ToString();
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            IntegrationTestHost.Configure(builder);
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Contentful:WebhookSecret"] = ContentfulToken,
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IHostedService>();
                foreach (var descriptor in services
                    .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                    .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(databaseName));
                services.RemoveAll<IContentfulContentCache>();
                services.AddSingleton<IContentfulContentCache>(cache);
                services.RemoveAll<IBackgroundJobService>();
                services.AddSingleton<IBackgroundJobService>(jobs);
            });
        });
    }

    private static async Task<int> CountReleasesAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await dbContext.Releases.CountAsync();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string payload, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("BOT", token);
        return client.SendAsync(request);
    }

    private sealed class TrackingContentfulContentCache : IContentfulContentCache
    {
        public int InvalidationCount { get; private set; }

        public Task<T> GetOrCreateAsync<T>(
            string key,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public void InvalidateAll() => InvalidationCount++;

        public void InvalidateForContentType(string contentTypeId) => InvalidationCount++;
    }

    private sealed class TrackingBackgroundJobService : IBackgroundJobService
    {
        public int EnqueueCount { get; private set; }

        public Task<long> EnqueueAsync(
            string jobType,
            object? payload = null,
            DateTime? runAt = null,
            CancellationToken cancellationToken = default)
        {
            EnqueueCount++;
            return Task.FromResult(1L);
        }
    }
}
