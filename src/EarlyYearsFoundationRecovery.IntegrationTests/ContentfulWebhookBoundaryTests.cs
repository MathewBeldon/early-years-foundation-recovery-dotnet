using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    [InlineData("/change", "updatedAt", "content_check")]
    [InlineData("/release", "completedAt", "new_module_release")]
    public async Task Valid_payload_persists_release_and_matching_queued_job(
        string path,
        string timestampProperty,
        string expectedJobType)
    {
        var cache = new TrackingContentfulContentCache();
        await using var factory = CreateFactory(cache, new TrackingBackgroundJobService());
        using var client = factory.CreateClient();
        var payload = JsonSerializer.Serialize(new
        {
            sys = new Dictionary<string, object?>
            {
                ["id"] = "delivery-1",
                [timestampProperty] = "2026-05-29T10:40:00Z",
            },
        });

        var response = await SendAsync(client, path, payload, ContentfulToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var release = await db.Releases.SingleAsync();
        var job = await db.BackgroundJobs.SingleAsync();
        Assert.Equal("delivery-1", release.Name);
        Assert.Equal(expectedJobType, job.JobType);
        Assert.Equal("queued", job.Status);
        Assert.Equal(0, job.Attempts);
        Assert.Equal(5, job.MaxAttempts);
        Assert.Null(job.LockedAt);
        Assert.Null(job.LockedBy);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.LastError);
        if (path == "/release")
        {
            using var jobPayload = JsonDocument.Parse(job.Payload);
            Assert.Equal(release.Id, jobPayload.RootElement.GetProperty("releaseId").GetInt64());
        }
        else
        {
            Assert.Equal("{}", job.Payload);
        }

        Assert.Equal(1, cache.InvalidationCount);
    }

    [Fact]
    public async Task Duplicate_valid_delivery_records_each_receipt_and_job()
    {
        var cache = new TrackingContentfulContentCache();
        await using var factory = CreateFactory(cache, new TrackingBackgroundJobService());
        using var client = factory.CreateClient();
        const string payload = "{\"sys\":{\"id\":\"release-1\",\"completedAt\":\"2026-05-29T10:40:00Z\"}}";

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/release", payload, ContentfulToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(client, "/release", payload, ContentfulToken)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var releases = await db.Releases.OrderBy(x => x.Id).ToListAsync();
        var jobs = await db.BackgroundJobs.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, releases.Count);
        Assert.Equal(2, jobs.Count);
        Assert.All(jobs, job => Assert.Equal("new_module_release", job.JobType));
        for (var index = 0; index < releases.Count; index++)
        {
            using var jobPayload = JsonDocument.Parse(jobs[index].Payload);
            Assert.Equal(releases[index].Id, jobPayload.RootElement.GetProperty("releaseId").GetInt64());
        }
    }

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
