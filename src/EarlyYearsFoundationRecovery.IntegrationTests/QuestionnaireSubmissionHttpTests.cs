using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class QuestionnaireSubmissionHttpTests : IAsyncLifetime
{
    private const string TestUserHeader = "X-Test-User-Id";
    private const string ModuleName = "module-2";
    private const string QuestionName = "summative-q1";

    private QuestionnaireWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private long _userId;

    public async Task InitializeAsync()
    {
        _factory = new QuestionnaireWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        _userId = await _factory.SeedScenarioAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Get_then_submit_first_answer_reuses_passed_assessment_and_records_Rails_events()
    {
        var page = await GetQuestionAsync();
        var token = Extract(page, "name=\"__RequestVerificationToken\"");
        var nonce = Extract(page, "name=\"response[submission_nonce]\"");

        using var request = NewRequest(HttpMethod.Post, $"/modules/{ModuleName}/responses/{QuestionName}");
        request.Content = Form(token, new Dictionary<string, string>
        {
            ["_method"] = "patch",
            ["response[submission_nonce]"] = nonce,
            ["response[answers]"] = "1",
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/modules/{ModuleName}/questionnaires/summative-q2", response.Headers.Location?.OriginalString);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assessment = Assert.Single(await db.Assessments.AsNoTracking().ToListAsync());
        Assert.Equal(75f, assessment.Score);
        Assert.True(assessment.Passed);
        Assert.NotNull(assessment.CompletedAt);

        var saved = Assert.Single(await db.Responses.AsNoTracking().ToListAsync());
        Assert.Equal(assessment.Id, saved.AssessmentId);
        Assert.Equal("module-2", saved.TrainingModule);
        Assert.Equal("summative-q1", saved.QuestionName);
        Assert.Equal("summative", saved.QuestionType);
        Assert.Equal(["1"], saved.Answers);
        Assert.True(saved.Correct);

        Assert.Empty(await db.UserModuleProgress.AsNoTracking()
            .Where(item => item.UserId == _userId && item.ModuleName == ModuleName)
            .ToListAsync());

        var start = Assert.Single(await db.Events.AsNoTracking()
            .Where(item => item.Name == "summative_assessment_start")
            .ToListAsync());
        Assert.Equal("module-2", PropertyString(start.Properties, "training_module_id"));
        Assert.Equal("summative-q1", PropertyString(start.Properties, "id"));
        Assert.Equal("module-2-5", PropertyString(start.Properties, "uid"));
        Assert.Equal("module-module-2", PropertyString(start.Properties, "mod_uid"));

        var answer = Assert.Single(await db.Events.AsNoTracking()
            .Where(item => item.Name == "questionnaire_answer")
            .ToListAsync());
        Assert.Equal("summative", PropertyString(answer.Properties, "type"));
        Assert.Equal("summative-q1", PropertyString(answer.Properties, "id"));
        Assert.Equal("module-2-5", PropertyString(answer.Properties, "uid"));
        Assert.Equal("module-module-2", PropertyString(answer.Properties, "mod_uid"));
        Assert.True(PropertyBool(answer.Properties, "success"));
        Assert.Equal(1, PropertyIntArray(answer.Properties, "answers").Single());
    }

    [Fact]
    public async Task First_summative_question_get_creates_an_incomplete_assessment_without_marking_progress()
    {
        var page = await GetQuestionAsync("module-4", "summative-q1");
        Assert.Contains("response[submission_nonce]", page, StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assessment = Assert.Single(await db.Assessments.AsNoTracking()
            .Where(item => item.UserId == _userId && item.TrainingModule == "module-4")
            .ToListAsync());

        Assert.Null(assessment.Score);
        Assert.Null(assessment.CompletedAt);
        Assert.Empty(await db.UserModuleProgress.AsNoTracking()
            .Where(item => item.UserId == _userId && item.ModuleName == "module-4")
            .ToListAsync());
    }

    [Fact]
    public async Task Final_summative_nonce_is_consumed_and_duplicate_replay_is_idempotent()
    {
        var firstPage = await GetQuestionAsync("module-4", "summative-q1");
        var firstToken = Extract(firstPage, "name=\"__RequestVerificationToken\"");
        var nonce = Extract(firstPage, "name=\"response[submission_nonce]\"");

        var firstResponse = await SubmitAnswerAsync(
            "module-4",
            "summative-q1",
            firstToken,
            nonce,
            "1");
        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
        Assert.Equal(
            "/modules/module-4/questionnaires/summative-q2",
            firstResponse.Headers.Location?.OriginalString);

        var secondPage = await GetQuestionAsync("module-4", "summative-q2");
        var secondToken = Extract(secondPage, "name=\"__RequestVerificationToken\"");
        var secondNonce = Extract(secondPage, "name=\"response[submission_nonce]\"");
        Assert.Equal(nonce, secondNonce);

        var secondResponse = await SubmitAnswerAsync(
            "module-4",
            "summative-q2",
            secondToken,
            secondNonce,
            "2");
        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);
        Assert.Equal(
            "/modules/module-4/questionnaires/summative-q3",
            secondResponse.Headers.Location?.OriginalString);

        var finalPage = await GetQuestionAsync("module-4", "summative-q3");
        var finalToken = Extract(finalPage, "name=\"__RequestVerificationToken\"");
        var finalNonce = Extract(finalPage, "name=\"response[submission_nonce]\"");
        Assert.Equal(nonce, finalNonce);

        var finalResponse = await SubmitAnswerAsync(
            "module-4",
            "summative-q3",
            finalToken,
            finalNonce,
            "1");
        Assert.Equal(HttpStatusCode.Redirect, finalResponse.StatusCode);
        Assert.Equal(
            "/modules/module-4/assessment-result/assessment-results",
            finalResponse.Headers.Location?.OriginalString);

        var beforeReplay = await ReadAssessmentStateAsync("module-4");

        var replayResponse = await SubmitAnswerAsync(
            "module-4",
            "summative-q3",
            finalToken,
            finalNonce,
            "1");
        Assert.Equal(HttpStatusCode.Redirect, replayResponse.StatusCode);
        Assert.Equal(
            "/modules/module-4/questionnaires/summative-q3",
            replayResponse.Headers.Location?.OriginalString);

        var afterReplay = await ReadAssessmentStateAsync("module-4");
        Assert.Equal(beforeReplay.Assessment, afterReplay.Assessment);
        Assert.Equal(beforeReplay.Responses.Keys, afterReplay.Responses.Keys);
        foreach (var responseId in beforeReplay.Responses.Keys)
        {
            Assert.Equal(beforeReplay.Responses[responseId].UpdatedAt, afterReplay.Responses[responseId].UpdatedAt);
            Assert.Equal(beforeReplay.Responses[responseId].Correct, afterReplay.Responses[responseId].Correct);
            Assert.Equal(beforeReplay.Responses[responseId].Answers, afterReplay.Responses[responseId].Answers);
        }

        Assert.Equal(3, afterReplay.QuestionnaireAnswerEvents);
        Assert.Equal(0, afterReplay.CompletionEvents);
    }

    [Fact]
    public async Task Missing_answer_returns_422_without_response_or_answer_event()
    {
        var page = await GetQuestionAsync();
        var token = Extract(page, "name=\"__RequestVerificationToken\"");
        var nonce = Extract(page, "name=\"response[submission_nonce]\"");

        using var request = NewRequest(HttpMethod.Post, $"/modules/{ModuleName}/responses/{QuestionName}");
        request.Content = Form(token, new Dictionary<string, string>
        {
            ["_method"] = "patch",
            ["response[submission_nonce]"] = nonce,
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Please select an answer", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertNoResponseOrAnswerEventAsync();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PATCH")]
    public async Task Invalid_nonce_on_post_or_patch_redirects_back_without_persisting_response_or_answer_event(string method)
    {
        var page = await GetQuestionAsync();
        var token = Extract(page, "name=\"__RequestVerificationToken\"");

        using var request = NewRequest(new HttpMethod(method), $"/modules/{ModuleName}/responses/{QuestionName}");
        request.Content = Form(token, new Dictionary<string, string>
        {
            ["_method"] = "patch",
            ["response[submission_nonce]"] = "wrong-nonce",
            ["response[answers]"] = "1",
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/modules/{ModuleName}/questionnaires/{QuestionName}", response.Headers.Location?.OriginalString);
        await AssertNoResponseOrAnswerEventAsync();
    }

    [Fact]
    public async Task Missing_antiforgery_token_is_rejected_without_persisting_response_or_answer_event()
    {
        var page = await GetQuestionAsync();
        var nonce = Extract(page, "name=\"response[submission_nonce]\"");

        using var request = NewRequest(HttpMethod.Post, $"/modules/{ModuleName}/responses/{QuestionName}");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["_method"] = "patch",
            ["response[submission_nonce]"] = nonce,
            ["response[answers]"] = "1",
        });

        var response = await _client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        await AssertNoResponseOrAnswerEventAsync();
    }

    private Task<string> GetQuestionAsync() => GetQuestionAsync(ModuleName, QuestionName);

    private async Task<string> GetQuestionAsync(string moduleName, string questionName)
    {
        using var request = NewRequest(HttpMethod.Get, $"/modules/{moduleName}/questionnaires/{questionName}");
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> SubmitAnswerAsync(
        string moduleName,
        string questionName,
        string token,
        string nonce,
        string answer)
    {
        using var request = NewRequest(HttpMethod.Post, $"/modules/{moduleName}/responses/{questionName}");
        request.Content = Form(token, new Dictionary<string, string>
        {
            ["_method"] = "patch",
            ["response[submission_nonce]"] = nonce,
            ["response[answers]"] = answer,
        });

        return await _client.SendAsync(request);
    }

    private async Task<AssessmentState> ReadAssessmentStateAsync(string moduleName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var assessment = await db.Assessments.AsNoTracking()
            .Where(item => item.UserId == _userId && item.TrainingModule == moduleName)
            .Select(item => new AssessmentSnapshot(
                item.Id,
                item.Score,
                item.Passed,
                item.StartedAt,
                item.CompletedAt))
            .SingleAsync();
        var responses = await db.Responses.AsNoTracking()
            .Where(item => item.UserId == _userId && item.TrainingModule == moduleName)
            .ToDictionaryAsync(
                item => item.Id,
                item => new ResponseSnapshot(item.UpdatedAt, item.Correct, item.Answers.ToArray()));
        var events = await db.Events.AsNoTracking()
            .Where(item => item.UserId == _userId)
            .ToListAsync();

        return new AssessmentState(
            assessment,
            responses,
            events.Count(item => item.Name == "questionnaire_answer"),
            events.Count(item => item.Name == "summative_assessment_complete"));
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestUserHeader, _userId.ToString());
        return request;
    }

    private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(fields);
    }

    private async Task AssertNoResponseOrAnswerEventAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Empty(await db.Responses.AsNoTracking().ToListAsync());
        Assert.Empty(await db.Events.AsNoTracking()
            .Where(item => item.Name == "questionnaire_answer")
            .ToListAsync());
    }

    private static string Extract(string html, string marker)
    {
        var markerIndex = html.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Expected form marker '{marker}'.");
        var valueStart = html.IndexOf("value=\"", markerIndex, StringComparison.Ordinal);
        Assert.True(valueStart >= 0, $"Expected value for form marker '{marker}'.");
        valueStart += "value=\"".Length;
        var valueEnd = html.IndexOf('"', valueStart);
        Assert.True(valueEnd >= 0, $"Expected value terminator for form marker '{marker}'.");
        return WebUtility.HtmlDecode(html[valueStart..valueEnd]);
    }

    private static string PropertyString(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties[key] switch
        {
            string value => value,
            System.Text.Json.JsonElement element => element.GetString() ?? string.Empty,
            { } value => value.ToString() ?? string.Empty,
            null => string.Empty,
        };

    private static bool PropertyBool(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties[key] switch
        {
            bool value => value,
            System.Text.Json.JsonElement element => element.GetBoolean(),
            _ => throw new InvalidOperationException($"Property '{key}' was not boolean."),
        };

    private static IReadOnlyList<int> PropertyIntArray(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties[key] switch
        {
            System.Text.Json.JsonElement element => element.EnumerateArray().Select(item => item.GetInt32()).ToArray(),
            IEnumerable<int> values => values.ToArray(),
            _ => throw new InvalidOperationException($"Property '{key}' was not an integer array."),
        };

    private sealed record AssessmentState(
        AssessmentSnapshot Assessment,
        IReadOnlyDictionary<long, ResponseSnapshot> Responses,
        int QuestionnaireAnswerEvents,
        int CompletionEvents);

    private sealed record AssessmentSnapshot(
        long Id,
        float? Score,
        bool? Passed,
        DateTime? StartedAt,
        DateTime? CompletedAt);

    private sealed record ResponseSnapshot(DateTime UpdatedAt, bool? Correct, string[] Answers);

    private sealed class QuestionnaireWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
            });
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection()
                    .UseEphemeralDataProtectionProvider();

                foreach (var descriptor in services
                    .Where(d => d.ServiceType == typeof(ApplicationDbContext)
                        || d.ServiceType == typeof(DbContextOptions)
                        || d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>))
                    .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = AuthConstants.Scheme;
                });
            });
        }

        public async Task<long> SeedScenarioAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Email = "questionnaire@example.test",
                RegistrationComplete = true,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            db.Assessments.Add(new Assessment
            {
                UserId = user.Id,
                TrainingModule = ModuleName,
                Score = 75,
                Passed = true,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                CompletedAt = DateTime.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync();
            return user.Id;
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "QuestionnaireTest";

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
                    new Claim(AuthConstants.EmailClaim, "questionnaire@example.test"),
                    new Claim(ClaimTypes.Name, "Questionnaire Learner"),
                ],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
