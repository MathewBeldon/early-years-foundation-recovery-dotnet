using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Training;
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

public sealed class AssessmentResultsTrackingHttpTests : IAsyncLifetime
{
    private const string TestUserHeader = "X-Test-User-Id";
    private const string ResultsPath = "/modules/module-1/assessment-result/assessment-results";
    private const string ModuleName = "module-1";

    private AssessmentResultsWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private long _userId;

    public async Task InitializeAsync()
    {
        _factory = new AssessmentResultsWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        _userId = await _factory.SeedUserAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Get_passed_results_records_summative_assessment_complete()
    {
        await SeedAssessmentAsync(score: 80, passed: true);

        var response = await GetResultsAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recorded = Assert.Single(await LoadEventsAsync());
        Assert.Equal(SummativeAssessmentCompleteTracking.EventName, recorded.Name);
        Assert.Equal(_userId, recorded.UserId);
        Assert.Equal("summative_assessment", PropertyString(recorded.Properties, "type"));
        Assert.Equal(ModuleName, PropertyString(recorded.Properties, "training_module_id"));
        Assert.Equal(80, PropertyNumber(recorded.Properties, "score"));
        Assert.True(PropertyBoolean(recorded.Properties, "success"));
        Assert.Equal("training/assessments", PropertyString(recorded.Properties, "controller"));
        Assert.Equal("show", PropertyString(recorded.Properties, "action"));
        Assert.Equal(ResultsPath, PropertyString(recorded.Properties, "path"));
    }

    [Fact]
    public async Task Get_failed_results_records_success_false()
    {
        await SeedAssessmentAsync(score: 45, passed: false);

        var response = await GetResultsAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var recorded = Assert.Single(await LoadEventsAsync());
        Assert.Equal(45, PropertyNumber(recorded.Properties, "score"));
        Assert.False(PropertyBoolean(recorded.Properties, "success"));
    }

    [Fact]
    public async Task Get_ungraded_results_does_not_record_an_event()
    {
        await SeedAssessmentAsync(score: null, passed: null);

        var response = await GetResultsAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await LoadEventsAsync());
    }

    [Fact]
    public async Task Repeated_get_of_failed_results_does_not_write_another_event()
    {
        // Rails v1.5.0 ac546721 writes a new failure event on every results GET
        // until a pass is stored. .NET keeps pass-once semantics and additionally
        // skips when the same success value is already present for the module.
        await SeedAssessmentAsync(score: 30, passed: false);

        var first = await GetResultsAsync();
        var second = await GetResultsAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Single(await LoadEventsAsync());
    }

    private async Task<HttpResponseMessage> GetResultsAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ResultsPath);
        request.Headers.Add(TestUserHeader, _userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task SeedAssessmentAsync(float? score, bool? passed)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Events.RemoveRange(db.Events);
        db.Visits.RemoveRange(db.Visits);
        db.Assessments.RemoveRange(db.Assessments);
        db.UserModuleProgress.RemoveRange(db.UserModuleProgress);
        db.Assessments.Add(new Assessment
        {
            UserId = _userId,
            TrainingModule = ModuleName,
            Score = score,
            Passed = passed,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
            CompletedAt = score is null ? null : DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<List<Event>> LoadEventsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Events
            .AsNoTracking()
            .Where(e => e.UserId == _userId && e.Name == SummativeAssessmentCompleteTracking.EventName)
            .ToListAsync();
    }

    private static string PropertyString(IReadOnlyDictionary<string, object?> properties, string key)
    {
        Assert.True(properties.ContainsKey(key));
        return properties[key] switch
        {
            string value => value,
            JsonElement element => element.GetString() ?? string.Empty,
            { } value => value.ToString() ?? string.Empty,
            null => string.Empty,
        };
    }

    private static double PropertyNumber(IReadOnlyDictionary<string, object?> properties, string key)
    {
        Assert.True(properties.ContainsKey(key));
        return properties[key] switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetDouble(),
            IConvertible value => Convert.ToDouble(value),
            _ => throw new InvalidOperationException($"Property '{key}' was not numeric."),
        };
    }

    private static bool PropertyBoolean(IReadOnlyDictionary<string, object?> properties, string key)
    {
        Assert.True(properties.ContainsKey(key));
        return properties[key] switch
        {
            bool value => value,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                element.GetBoolean(),
            _ => throw new InvalidOperationException($"Property '{key}' was not boolean."),
        };
    }

    private sealed class AssessmentResultsWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                    .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                    .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));

                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                        TestAuthHandler.SchemeName,
                        _ => { });
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
            var user = new User
            {
                Email = "assessment-results@example.test",
                RegistrationComplete = true,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "AssessmentResultsTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestUserHeader, out var values)
                || !long.TryParse(values.ToString(), out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(AuthConstants.UserIdClaim, userId.ToString()),
                    new Claim(AuthConstants.EmailClaim, "assessment-results@example.test"),
                    new Claim(ClaimTypes.Name, "Assessment Results Learner"),
                ],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
