using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Encodings.Web;
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

/// <summary>
/// HTTP conformance for the course feedback contract in Rails v1.5.0
/// (ac546721 app/controllers/feedback_controller.rb and its controller/system specs).
/// </summary>
public sealed class CourseFeedbackHttpTests
{
    private const string UserHeader = "X-Test-User-Id";

    [Fact]
    public async Task Public_intro_is_available_but_questions_require_a_registered_user()
    {
        await using var factory = new FeedbackWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var intro = await client.GetAsync("/feedback");
        var question = await client.GetAsync("/feedback/feedback-radio-only");

        Assert.Equal(HttpStatusCode.OK, intro.StatusCode);
        Assert.Contains("Give feedback", await intro.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Redirect, question.StatusCode);
        Assert.StartsWith("/users/sign-in", question.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authenticated_course_journey_validates_sequences_persists_and_completes()
    {
        await using var factory = new FeedbackWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var userId = await factory.SeedUserAsync();

        var first = await GetAsync(client, userId, "/feedback/feedback-radio-only");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Contains("Overall experience", await first.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var invalidRadio = await PostAsync(client, userId, "/feedback/feedback-radio-only");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidRadio.StatusCode);
        Assert.Contains("Select an answer.", await invalidRadio.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await factory.LoadFeedbackEventsAsync(userId));

        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-radio-only", ("SelectedAnswers", "1")),
            "/feedback/feedback-checkbox-only");
        var start = Assert.Single(await factory.LoadFeedbackEventsAsync(userId));
        AssertFeedbackEvent(start, "feedback_start", "/feedback/feedback-radio-only", "update", "feedback-radio-only");
        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-checkbox-only", ("SelectedAnswers", "0"), ("SelectedAnswers", "2")),
            "/feedback/feedback-textarea-only");
        Assert.Single(await factory.LoadFeedbackEventsAsync(userId));

        var invalidText = await PostAsync(client, userId, "/feedback/feedback-textarea-only", ("TextInput", "  "));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidText.StatusCode);
        Assert.Contains("Enter your answer.", await invalidText.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-textarea-only", ("TextInput", "  Useful course  ")),
            "/feedback/feedback-radio-other-more");

        var invalidOther = await PostAsync(
            client, userId, "/feedback/feedback-radio-other-more", ("SelectedAnswers", "4"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidOther.StatusCode);
        Assert.Contains("Enter details for your Other answer.", await invalidOther.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-radio-other-more", ("SelectedAnswers", "4"), ("TextInput", "  Other duration  ")),
            "/feedback/feedback-checkbox-other-more");

        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-checkbox-other-more", ("SelectedAnswers", "0")),
            "/feedback/feedback-radio-more");
        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-radio-more", ("SelectedAnswers", "0")),
            "/feedback/feedback-checkbox-other-or");
        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-checkbox-other-or", ("SelectedAnswers", "or")),
            "/feedback/feedback-skippable");
        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-skippable", ("SelectedAnswers", "1")),
            "/feedback/thank-you");

        var thankYou = await GetAsync(client, userId, "/feedback/thank-you");
        Assert.Equal(HttpStatusCode.OK, thankYou.StatusCode);
        Assert.Contains("Thank you", await thankYou.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        var events = await factory.LoadFeedbackEventsAsync(userId);
        Assert.Equal(2, events.Count);
        AssertFeedbackEvent(events[1], "feedback_complete", "/feedback/thank-you", "show", "thank-you");

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(client, userId, "/feedback/thank-you")).StatusCode);
        Assert.Equal(2, (await factory.LoadFeedbackEventsAsync(userId)).Count);

        var completeIntro = await GetAsync(client, userId, "/feedback");
        Assert.Contains("already submitted feedback", await completeIntro.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var responses = await db.Responses.AsNoTracking()
            .Where(response => response.UserId == userId && response.TrainingModule == "course")
            .OrderBy(response => response.QuestionName)
            .ToListAsync();
        Assert.Equal(8, responses.Count);
        Assert.All(responses, response =>
        {
            Assert.Equal("feedback", response.QuestionType);
            Assert.True(response.Correct);
            Assert.Null(response.VisitId);
        });
        Assert.Equal(["0", "2"], responses.Single(r => r.QuestionName == "feedback-checkbox-only").Answers);
        Assert.Equal("Useful course", responses.Single(r => r.QuestionName == "feedback-textarea-only").TextInput);
        Assert.Equal("Other duration", responses.Single(r => r.QuestionName == "feedback-radio-other-more").TextInput);
        Assert.Equal(["or"], responses.Single(r => r.QuestionName == "feedback-checkbox-other-or").Answers);
        Assert.True((await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId)).ResearchParticipant);
    }

    [Fact]
    public async Task Profile_mode_is_limited_to_the_skippable_question_and_returns_to_account()
    {
        await using var factory = new FeedbackWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var userId = await factory.SeedUserAsync();

        var redirect = await GetAsync(client, userId, "/feedback/feedback-radio-only?from=profile");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.Equal("/feedback/feedback-skippable?from=profile", redirect.Headers.Location?.OriginalString);

        var profile = await GetAsync(client, userId, "/feedback/feedback-skippable?from=profile");
        var body = await profile.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        Assert.Contains("name=\"From\" value=\"profile\"", body, StringComparison.Ordinal);
        Assert.Contains(">Save<", body, StringComparison.Ordinal);
        Assert.DoesNotContain(">Previous<", body, StringComparison.Ordinal);

        await AssertRedirectAsync(
            await PostAsync(client, userId, "/feedback/feedback-skippable", ("From", "profile"), ("SelectedAnswers", "0")),
            "/my-account");
        Assert.Empty(await factory.LoadFeedbackEventsAsync(userId));
    }

    [Fact]
    public async Task Unknown_and_thank_you_posts_are_not_found()
    {
        await using var factory = new FeedbackWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var userId = await factory.SeedUserAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(client, userId, "/feedback/not-a-question")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(client, userId, "/feedback/not-a-question")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await PostAsync(client, userId, "/feedback/thank-you")).StatusCode);
    }

    [Fact]
    public async Task Missing_user_is_rejected_without_writing_feedback_events()
    {
        await using var factory = new FeedbackWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await GetAsync(client, 999_999, "/feedback/thank-you");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/sign-in", response.Headers.Location?.OriginalString);
        Assert.Empty(await factory.LoadFeedbackEventsAsync(999_999));
    }

    private static void AssertFeedbackEvent(
        Event recorded,
        string name,
        string path,
        string action,
        string id)
    {
        Assert.Equal(name, recorded.Name);
        Assert.Equal(4, recorded.Properties.Count);
        Assert.Equal(path, PropertyString(recorded.Properties, "path"));
        Assert.Equal("feedback", PropertyString(recorded.Properties, "controller"));
        Assert.Equal(action, PropertyString(recorded.Properties, "action"));
        Assert.Equal(id, PropertyString(recorded.Properties, "id"));
    }

    private static string PropertyString(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties[key] switch
        {
            string value => value,
            JsonElement element => element.GetString() ?? string.Empty,
            { } value => value.ToString() ?? string.Empty,
            null => string.Empty,
        };

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, long userId, string path)
    {
        using var request = NewRequest(HttpMethod.Get, path, userId);
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        long userId,
        string path,
        params (string Name, string Value)[] values)
    {
        var tokenResponse = await GetAsync(client, userId, path);
        var token = ExtractAntiforgeryToken(await tokenResponse.Content.ReadAsStringAsync());
        using var request = NewRequest(HttpMethod.Post, path, userId);
        request.Content = new FormUrlEncodedContent(
            values.Select(value => new KeyValuePair<string, string>(value.Name, value.Value))
                .Append(new KeyValuePair<string, string>("__RequestVerificationToken", token)));
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage NewRequest(HttpMethod method, string path, long userId)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(UserHeader, userId.ToString());
        return request;
    }

    private static string ExtractAntiforgeryToken(string body)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var markerIndex = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, "Expected an antiforgery token in the feedback form.");
        var valueStart = body.IndexOf("value=\"", markerIndex, StringComparison.Ordinal) + "value=\"".Length;
        var valueEnd = body.IndexOf('"', valueStart);
        return WebUtility.HtmlDecode(body[valueStart..valueEnd]);
    }

    private static Task AssertRedirectAsync(HttpResponseMessage response, string expected)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
        return Task.CompletedTask;
    }

    private sealed class FeedbackWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                foreach (var descriptor in services.Where(descriptor =>
                    descriptor.ServiceType == typeof(ApplicationDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)).ToList())
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

        public async Task<long> SeedUserAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Email = $"feedback-{Guid.NewGuid():N}@example.test",
                GovOneId = $"feedback-{Guid.NewGuid():N}",
                FirstName = "Course",
                LastName = "Feedback",
                Country = "England",
                TermsAndConditionsAgreedAt = DateTime.UtcNow,
                RegistrationComplete = true,
                ResearchParticipant = false,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return user.Id;
        }

        public async Task<List<Event>> LoadFeedbackEventsAsync(long userId)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            return await db.Events.AsNoTracking()
                .Where(item => item.UserId == userId
                    && (item.Name == "feedback_start" || item.Name == "feedback_complete"))
                .OrderBy(item => item.Id)
                .ToListAsync();
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CourseFeedbackTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var values)
                || !long.TryParse(values.ToString(), out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(AuthConstants.UserIdClaim, userId.ToString())],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
