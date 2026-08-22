using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class ModuleContentTrackingHttpTests : IAsyncLifetime
{
    private const string UserHeader = "X-Test-User-Id";
    private ModuleContentFactory _factory = null!;
    private HttpClient _client = null!;
    private long _userId;

    public async Task InitializeAsync()
    {
        _factory = new ModuleContentFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _userId = await _factory.SeedUserAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Normal_content_records_idempotent_progress_and_Rails_KPI_events()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/modules/module-1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/modules/module-1")).StatusCode);

        var index = await GetAsync("/modules/module-1/content-pages");
        Assert.Equal(HttpStatusCode.Redirect, index.StatusCode);
        Assert.Equal("/modules/module-1/content-pages/what-to-expect", index.Headers.Location?.OriginalString);

        foreach (var page in new[] { "what-to-expect", "module-1-introduction", "key-concepts" })
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync($"/modules/module-1/content-pages/{page}")).StatusCode);
        }

        var firstProgress = await LoadProgressAsync();
        var originalStartedAt = firstProgress.StartedAt;
        var originalKeyConceptsVisit = firstProgress.VisitedPages["key-concepts"];

        foreach (var page in new[] { "applying-learning", "module-1-introduction", "key-concepts" })
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync($"/modules/module-1/content-pages/{page}")).StatusCode);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var progress = Assert.Single(await db.UserModuleProgress.AsNoTracking().Where(x => x.UserId == _userId).ToListAsync());
        Assert.Equal("key-concepts", progress.LastPage);
        Assert.Null(progress.CompletedAt);
        Assert.Equal(4, progress.VisitedPages.Count);
        Assert.Equal(["applying-learning", "key-concepts", "module-1-introduction", "what-to-expect"], progress.VisitedPages.Keys.Order().ToArray());
        Assert.Equal(originalStartedAt, progress.StartedAt);
        Assert.Equal(originalKeyConceptsVisit, progress.VisitedPages["key-concepts"]);

        var events = await db.Events.AsNoTracking().Where(x => x.UserId == _userId).ToListAsync();
        Assert.Equal(2, events.Count(x => x.Name == "module_overview_page"));
        var start = Assert.Single(events, x => x.Name == "module_start");
        Assert.Equal("training/pages", Property(start, "controller"));
        Assert.Equal("show", Property(start, "action"));
        Assert.Equal("module-1", Property(start, "training_module_id"));
        Assert.Equal("module-1-introduction", Property(start, "id"));
        Assert.Equal("sub_module_intro", Property(start, "type"));
        Assert.DoesNotContain(events, x => x.Name is "page_view" or "module_content_page");
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(UserHeader, _userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<UserModuleProgress> LoadProgressAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .UserModuleProgress.AsNoTracking().SingleAsync(x => x.UserId == _userId);
    }

    private static string Property(Event item, string key) => item.Properties[key] switch
    {
        JsonElement value => value.GetString() ?? string.Empty,
        { } value => value.ToString() ?? string.Empty,
        _ => string.Empty,
    };

    private sealed class ModuleContentFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            IntegrationTestHost.Configure(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(d => d.ServiceType == typeof(ApplicationDbContext)
                    || d.ServiceType == typeof(DbContextOptions)
                    || d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)).ToList())
                    services.Remove(descriptor);

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = AuthConstants.Scheme;
                });
            });
        }

        public async Task<long> SeedUserAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new User { Email = "module-content-http@example.test", RegistrationComplete = true });
            await db.SaveChangesAsync();
            return await db.Users.Select(x => x.Id).SingleAsync();
        }
    }

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "ModuleContentTest";
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var values) || !long.TryParse(values, out var userId))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(AuthConstants.UserIdClaim, userId.ToString()), new Claim(AuthConstants.EmailClaim, "module-content-http@example.test")], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
