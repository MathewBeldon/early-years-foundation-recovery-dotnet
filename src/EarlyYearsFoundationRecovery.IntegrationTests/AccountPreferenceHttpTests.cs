using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class AccountPreferenceHttpTests : IAsyncLifetime
{
    private const string UserHeader = "X-Test-User-Id";
    private const string TrainingPath = "/registration/training-emails/edit";
    private const string ResearchPath = "/registration/research-participant/edit";

    private AccountPreferenceWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private long _userId;

    public async Task InitializeAsync()
    {
        _factory = new AccountPreferenceWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _userId = await _factory.SeedUserAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Get_renders_the_current_training_and_research_selections()
    {
        var training = await GetAsync(TrainingPath);
        var research = await GetAsync(ResearchPath);

        Assert.Equal(HttpStatusCode.OK, training.StatusCode);
        Assert.Equal(HttpStatusCode.OK, research.StatusCode);
        Assert.Contains("name=\"TrainingEmails\"", await training.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("name=\"ResearchParticipant\"", await research.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        AssertChecked(await training.Content.ReadAsStringAsync(), "TrainingEmails");
        AssertChecked(await research.Content.ReadAsStringAsync(), "ResearchParticipant");
    }

    [Fact]
    public async Task Terms_success_writes_the_Rails_registration_event()
    {
        var token = await GetTokenAsync("/registration/terms-and-conditions/edit");
        using var request = NewRequest(HttpMethod.Post, "/registration/terms-and-conditions");
        request.Content = Form(token, ("Accepted", "true"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/my-account", response.Headers.Location?.OriginalString);
        await AssertRegistrationEventAsync(
            RegistrationEventTracker.TermsAndConditionsEvent,
            "registration/terms_and_conditions",
            success: true,
            expectedPath: "/registration/terms-and-conditions");
    }

    [Fact]
    public async Task Terms_failure_writes_only_the_failed_Rails_registration_event()
    {
        var token = await GetTokenAsync("/registration/terms-and-conditions/edit");
        using var request = NewRequest(HttpMethod.Post, "/registration/terms-and-conditions");
        request.Content = Form(token, ("Accepted", "false"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertRegistrationEventAsync(
            RegistrationEventTracker.TermsAndConditionsEvent,
            "registration/terms_and_conditions",
            success: false,
            expectedPath: "/registration/terms-and-conditions");
    }

    [Fact]
    public async Task Flat_POST_training_emails_updates_the_value_and_writes_the_Rails_event()
    {
        var token = await GetTokenAsync(TrainingPath);
        using var request = NewRequest(HttpMethod.Post, "/registration/training-emails");
        request.Content = Form(token, ("TrainingEmails", "false"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/my-account", response.Headers.Location?.OriginalString);
        await AssertPreferenceAndEventAsync(
            trainingEmails: false,
            researchParticipant: true,
            eventName: RegistrationPreferenceEventTracker.TrainingEmailsEvent,
            success: true,
            controller: "registration/training_emails");
    }

    [Fact]
    public async Task Nested_PATCH_research_participant_updates_the_value_and_writes_the_Rails_event()
    {
        var token = await GetTokenAsync(ResearchPath);
        using var request = NewRequest(HttpMethod.Patch, "/registration/research-participant");
        request.Content = Form(token, ("user[research_participant]", "false"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/my-account", response.Headers.Location?.OriginalString);
        await AssertPreferenceAndEventAsync(
            trainingEmails: true,
            researchParticipant: false,
            eventName: RegistrationPreferenceEventTracker.ResearchParticipantEvent,
            success: true,
            controller: "registration/research_participants");
    }

    [Fact]
    public async Task Missing_training_selection_returns_422_without_mutating_the_preference_and_writes_only_the_failure_event()
    {
        var token = await GetTokenAsync(TrainingPath);
        using var request = NewRequest(HttpMethod.Post, "/registration/training-emails");
        request.Content = Form(token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Choose an option.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertPreferenceAndEventAsync(
            trainingEmails: true,
            researchParticipant: true,
            eventName: RegistrationPreferenceEventTracker.TrainingEmailsEvent,
            success: false,
            controller: "registration/training_emails");
    }

    [Fact]
    public async Task Invalid_nested_research_selection_returns_422_without_mutating_the_preference_and_writes_only_the_failure_event()
    {
        var token = await GetTokenAsync(ResearchPath);
        using var request = NewRequest(HttpMethod.Patch, "/registration/research-participant");
        request.Content = Form(token, ("user[research_participant]", "not-a-boolean"));

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("Choose an option.", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        await AssertPreferenceAndEventAsync(
            trainingEmails: true,
            researchParticipant: true,
            eventName: RegistrationPreferenceEventTracker.ResearchParticipantEvent,
            success: false,
            controller: "registration/research_participants");
    }

    [Fact]
    public async Task Completing_registration_writes_check_your_answers_and_registration_once()
    {
        _userId = await _factory.SeedUserAsync(registrationComplete: false);
        var token = await GetTokenAsync("/registration/check-your-answers/edit");

        using var firstRequest = NewRequest(HttpMethod.Post, "/registration/check-your-answers");
        firstRequest.Content = Form(token);
        var firstResponse = await _client.SendAsync(firstRequest);

        Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);

        var secondToken = await GetTokenAsync("/registration/check-your-answers/edit");
        using var secondRequest = NewRequest(HttpMethod.Post, "/registration/check-your-answers");
        secondRequest.Content = Form(secondToken);
        var secondResponse = await _client.SendAsync(secondRequest);

        Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var events = await db.Events.AsNoTracking().Where(item => item.UserId == _userId).ToListAsync();
        Assert.Equal(2, events.Count(item => item.Name == RegistrationEventTracker.CheckYourAnswersEvent));
        Assert.Single(events, item => item.Name == RegistrationEventTracker.RegistrationEvent);
        Assert.All(
            events.Where(item => item.Name is RegistrationEventTracker.CheckYourAnswersEvent or RegistrationEventTracker.RegistrationEvent),
            item =>
            {
                Assert.Equal("registration/check_your_answers", PropertyString(item.Properties, "controller"));
                Assert.Equal("update", PropertyString(item.Properties, "action"));
                Assert.Equal("/registration/check-your-answers", PropertyString(item.Properties, "path"));
                Assert.True(PropertyBool(item.Properties, "success"));
            });
    }

    private async Task<HttpResponseMessage> GetAsync(string path)
    {
        using var request = NewRequest(HttpMethod.Get, path);
        return await _client.SendAsync(request);
    }

    private async Task<string> GetTokenAsync(string path)
    {
        var response = await GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var marker = "name=\"__RequestVerificationToken\"";
        var markerIndex = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Expected {marker} in {path}.");
        var valueStart = body.IndexOf("value=\"", markerIndex, StringComparison.Ordinal);
        Assert.True(valueStart >= 0, $"Expected antiforgery token value in {path}.");
        valueStart += "value=\"".Length;
        var valueEnd = body.IndexOf('"', valueStart);
        Assert.True(valueEnd >= 0, $"Expected antiforgery token terminator in {path}.");
        return WebUtility.HtmlDecode(body[valueStart..valueEnd]);
    }

    private HttpRequestMessage NewRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add(UserHeader, _userId.ToString());
        return request;
    }

    private static FormUrlEncodedContent Form(string token, params (string Name, string Value)[] values)
    {
        var form = values.ToDictionary(pair => pair.Name, pair => pair.Value);
        form["__RequestVerificationToken"] = token;
        return new FormUrlEncodedContent(form);
    }

    private async Task AssertPreferenceAndEventAsync(
        bool trainingEmails,
        bool researchParticipant,
        string eventName,
        bool success,
        string controller)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(item => item.Id == _userId);
        Assert.Equal(trainingEmails, user.TrainingEmails);
        Assert.Equal(researchParticipant, user.ResearchParticipant);

        var events = await db.Events.AsNoTracking().Where(item => item.UserId == _userId).ToListAsync();
        var preferenceEvent = Assert.Single(events, item => item.Name == eventName);
        Assert.Equal(4, preferenceEvent.Properties.Count);
        Assert.Equal("/registration/" + (eventName == RegistrationPreferenceEventTracker.TrainingEmailsEvent
            ? "training-emails"
            : "research-participant"), PropertyString(preferenceEvent.Properties, "path"));
        Assert.Equal(controller, PropertyString(preferenceEvent.Properties, "controller"));
        Assert.Equal("update", PropertyString(preferenceEvent.Properties, "action"));
        Assert.Equal(success, PropertyBool(preferenceEvent.Properties, "success"));
        Assert.DoesNotContain(events, item =>
            item.Name == (eventName == RegistrationPreferenceEventTracker.TrainingEmailsEvent
                ? RegistrationPreferenceEventTracker.ResearchParticipantEvent
                : RegistrationPreferenceEventTracker.TrainingEmailsEvent));
    }

    private async Task AssertRegistrationEventAsync(
        string eventName,
        string controller,
        bool success,
        string expectedPath)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var events = await db.Events.AsNoTracking().Where(item => item.UserId == _userId).ToListAsync();
        var registrationEvent = Assert.Single(events, item => item.Name == eventName);
        Assert.Equal(4, registrationEvent.Properties.Count);
        Assert.Equal(expectedPath, PropertyString(registrationEvent.Properties, "path"));
        Assert.Equal(controller, PropertyString(registrationEvent.Properties, "controller"));
        Assert.Equal("update", PropertyString(registrationEvent.Properties, "action"));
        Assert.Equal(success, PropertyBool(registrationEvent.Properties, "success"));
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

    private static void AssertChecked(string body, string fieldName)
    {
        var fieldMarker = $@"name=""{fieldName}""";
        var inputStart = 0;
        var foundTrueInput = false;
        while ((inputStart = body.IndexOf("<input", inputStart, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var inputEnd = body.IndexOf('>', inputStart);
            Assert.True(inputEnd > inputStart, $"Expected input element for {fieldName}.");
            var input = body[inputStart..(inputEnd + 1)];
            if (input.Contains(fieldMarker, StringComparison.OrdinalIgnoreCase)
                && input.Contains("value=\"true\"", StringComparison.OrdinalIgnoreCase))
            {
                foundTrueInput = true;
                Assert.True(
                    input.Contains("checked", StringComparison.OrdinalIgnoreCase),
                    $"True radio for {fieldName} was not checked: {input}");
                break;
            }

            inputStart = inputEnd + 1;
        }

        Assert.True(foundTrueInput, $"Expected true radio field {fieldName}.");
    }

    private sealed class AccountPreferenceWebApplicationFactory : WebApplicationFactory<Program>
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

        public async Task<long> SeedUserAsync(bool registrationComplete = true)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Email = "account-preferences@example.test",
                GovOneId = "synthetic-account-preferences",
                FirstName = "Account",
                LastName = "Preferences",
                Country = "England",
                TermsAndConditionsAgreedAt = DateTime.UtcNow,
                RegistrationComplete = registrationComplete,
                TrainingEmails = true,
                ResearchParticipant = true,
                SettingTypeId = "other",
                SettingType = "other",
                SettingTypeOther = "Other setting",
                LocalAuthority = "N/A",
                RoleType = "general_role_1",
                EarlyYearsExperience = "0-2",
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
        public const string SchemeName = "AccountPreferenceTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var values)
                || !long.TryParse(values.ToString(), out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(AuthConstants.UserIdClaim, userId.ToString()),
                    new Claim(AuthConstants.EmailClaim, "account-preferences@example.test"),
                    new Claim(ClaimTypes.Name, "Account Preferences"),
                ],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
