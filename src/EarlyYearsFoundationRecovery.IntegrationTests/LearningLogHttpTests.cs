using System.Globalization;
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

public sealed class LearningLogHttpTests : IAsyncLifetime
{
    private const string TestUserHeader = "X-Test-User-Id";

    private LearningLogWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private long _registeredUserId;
    private long _otherRegisteredUserId;
    private long _incompleteUserId;

    public async Task InitializeAsync()
    {
        _factory = new LearningLogWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });
        (_registeredUserId, _otherRegisteredUserId, _incompleteUserId) = await _factory.SeedUsersAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PATCH")]
    [InlineData("PUT")]
    public async Task Save_upserts_note_for_rails_verbs_and_redirects_to_local_next_path(string method)
    {
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);
        var pageName = $"intro-{method.ToLowerInvariant()}";
        var nextPath = $"/modules/module-1/content-pages/{pageName}-next";

        var response = await SendSaveAsync(
            method,
            _registeredUserId,
            token,
            new Dictionary<string, string>
            {
                ["Title"] = "Reflection",
                ["Body"] = $"{method} notes",
                ["TrainingModule"] = "module-1",
                ["Name"] = pageName,
                ["NextPageUrl"] = nextPath,
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(nextPath, response.Headers.Location?.OriginalString);

        var note = await FindNoteAsync(_registeredUserId, "module-1", pageName);
        Assert.NotNull(note);
        Assert.Equal($"{method} notes", note!.Body);
        Assert.Equal("Reflection", note.Title);
    }

    [Fact]
    public async Task Save_patch_updates_existing_note_without_duplicating()
    {
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);
        const string pageName = "existing-page";
        var fields = new Dictionary<string, string>
        {
            ["Title"] = "First title",
            ["Body"] = "First draft",
            ["TrainingModule"] = "module-1",
            ["Name"] = pageName,
            ["NextPageUrl"] = "/my-modules",
        };

        var created = await SendSaveAsync("POST", _registeredUserId, token, fields);
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        fields["Body"] = "Edited reflection";
        fields["Title"] = "Updated title";
        var updated = await SendSaveAsync("PATCH", _registeredUserId, token, fields);

        Assert.Equal(HttpStatusCode.Redirect, updated.StatusCode);
        Assert.Equal("/my-modules", updated.Headers.Location?.OriginalString);

        await using var scope = _factory.Services.CreateAsyncScope();
        var notes = await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Notes
            .Where(n => n.UserId == _registeredUserId && n.Name == pageName)
            .ToListAsync();
        var note = Assert.Single(notes);
        Assert.Equal("Edited reflection", note.Body);
        Assert.Equal("Updated title", note.Title);

        var events = await FindEventsAsync(_registeredUserId);
        Assert.Contains(events, e => e.Name == "user_note_created");
        var updatedEvent = Assert.Single(events, e => e.Name == "user_note_updated");
        AssertRailsNoteEvent(updatedEvent, "update", "Edited reflection", "Updated title", pageName);
    }

    [Fact]
    public async Task Save_new_note_writes_the_Rails_created_event_shape()
    {
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);

        var response = await SendSaveAsync(
            "POST",
            _registeredUserId,
            token,
            new Dictionary<string, string>
            {
                ["Title"] = "Reflection",
                ["Body"] = "A reflection",
                ["TrainingModule"] = "module-1",
                ["Name"] = "event-create",
                ["NextPageName"] = "next-page",
                ["NextPageModule"] = "module-1",
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var createdEvent = Assert.Single(await FindEventsAsync(_registeredUserId), e => e.Name == "user_note_created");
        AssertRailsNoteEvent(createdEvent, "create", "A reflection", "Reflection", "event-create");
        Assert.Equal("next-page", PropertyValue(createdEvent, "next_page_name"));
        Assert.Equal("module-1", PropertyValue(createdEvent, "next_page_module"));
    }

    [Fact]
    public async Task Repeated_post_upserts_but_keeps_the_Rails_create_event_semantics()
    {
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);
        var fields = new Dictionary<string, string>
        {
            ["Title"] = "Reflection",
            ["Body"] = "First draft",
            ["TrainingModule"] = "module-1",
            ["Name"] = "repeated-post",
        };

        await SendSaveAsync("POST", _registeredUserId, token, fields);
        fields["Body"] = "Second draft";
        await SendSaveAsync("POST", _registeredUserId, token, fields);

        Assert.Equal("Second draft", (await FindNoteAsync(_registeredUserId, "module-1", "repeated-post"))!.Body);
        var events = await FindEventsAsync(_registeredUserId);
        Assert.Equal(2, events.Count(e => e.Name == "user_note_created"));
        Assert.DoesNotContain(events, e => e.Name == "user_note_updated");
    }

    [Fact]
    public async Task Rendered_new_and_existing_note_forms_use_the_Rails_method_shape()
    {
        var newPage = await GetTrainingPageAsync("key-concepts");
        Assert.Contains("<form", newPage);
        Assert.Contains("method=\"post\"", newPage);
        Assert.Contains("action=\"/my-account/learning-log\"", newPage);
        Assert.DoesNotContain("name=\"_method\"", newPage);

        var token = await GetAntiForgeryTokenAsync(_registeredUserId);
        await SendSaveAsync("POST", _registeredUserId, token, new Dictionary<string, string>
        {
            ["Title"] = "Topic intro page",
            ["Body"] = "Existing note",
            ["TrainingModule"] = "module-1",
            ["Name"] = "key-concepts",
        });

        var existingPage = await GetTrainingPageAsync("key-concepts");
        Assert.Contains("<form", existingPage);
        Assert.Contains("method=\"post\"", existingPage);
        Assert.Contains("action=\"/my-account/learning-log\"", existingPage);
        Assert.Contains("name=\"_method\" value=\"patch\"", existingPage);
    }

    [Fact]
    public async Task Learning_log_renders_multiline_note_text_encoded_and_formatted()
    {
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);
        await SendSaveAsync("POST", _registeredUserId, token, new Dictionary<string, string>
        {
            ["Title"] = "Topic intro page",
            ["Body"] = "First line\nsecond line\n\n<script>alert('xss')</script>",
            ["TrainingModule"] = "module-1",
            ["Name"] = "key-concepts",
        });

        var response = await SendAsUserAsync(HttpMethod.Get, "/my-account/learning-log", _registeredUserId);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("First line<br />\nsecond line", html, StringComparison.Ordinal);
        Assert.Contains("<p class=\"govuk-body learning-log-entry\" data-clarity-mask=\"True\">&lt;script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Saving_a_page_cannot_update_another_users_note()
    {
        await _factory.SeedNoteAsync(_otherRegisteredUserId, "module-1", "owned-by-other", "Other user's note");
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);

        var response = await SendSaveAsync("POST", _registeredUserId, token, new Dictionary<string, string>
        {
            ["Title"] = "Attempted takeover",
            ["Body"] = "Should remain separate",
            ["TrainingModule"] = "module-1",
            ["Name"] = "owned-by-other",
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var otherNote = await FindNoteAsync(_otherRegisteredUserId, "module-1", "owned-by-other");
        Assert.Equal("Other user's note", otherNote?.Body);
        Assert.Equal("Should remain separate", (await FindNoteAsync(_registeredUserId, "module-1", "owned-by-other"))?.Body);
    }

    [Fact]
    public async Task Save_rejects_external_next_page_url_and_uses_rails_fallback()
    {
        var token = await GetAntiForgeryTokenAsync(_registeredUserId);

        var response = await SendSaveAsync(
            "POST",
            _registeredUserId,
            token,
            new Dictionary<string, string>
            {
                ["Title"] = "Safe redirect",
                ["Body"] = "Keep this local",
                ["TrainingModule"] = "module-1",
                ["Name"] = "open-redirect",
                ["NextPageUrl"] = "https://evil.example/phish",
                ["NextPageModule"] = "module-1",
                ["NextPageName"] = "applying-learning",
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "/modules/module-1/content-pages/applying-learning",
            response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PATCH")]
    [InlineData("PUT")]
    public async Task Save_redirects_to_sign_in_when_not_authenticated(string method)
    {
        var token = await GetAntiForgeryTokenAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/my-account/learning-log")
        {
            Content = Form(token, new Dictionary<string, string>
            {
                ["TrainingModule"] = "module-1",
                ["Name"] = "unauthenticated",
                ["Body"] = "should not persist",
            }),
        };

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/users/sign-in", response.Headers.Location?.OriginalString, StringComparison.OrdinalIgnoreCase);
        Assert.Null(await FindNoteAsync(_registeredUserId, "module-1", "unauthenticated"));
    }

    [Fact]
    public async Task Save_put_redirects_incomplete_registration_to_the_current_step()
    {
        var token = await GetAntiForgeryTokenAsync(_incompleteUserId);

        var response = await SendSaveAsync(
            "PUT",
            _incompleteUserId,
            token,
            new Dictionary<string, string>
            {
                ["TrainingModule"] = "module-1",
                ["Name"] = "incomplete-user",
                ["Body"] = "should not persist",
                ["NextPageUrl"] = "/my-modules",
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/registration/terms-and-conditions", response.Headers.Location?.OriginalString);
        Assert.Null(await FindNoteAsync(_incompleteUserId, "module-1", "incomplete-user"));
    }

    [Fact]
    public async Task Save_rejects_post_without_anti_forgery_token()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/my-account/learning-log")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["TrainingModule"] = "module-1",
                ["Name"] = "csrf",
                ["Body"] = "should not persist",
            }),
        };
        request.Headers.Add(TestUserHeader, _registeredUserId.ToString());

        var response = await _client.SendAsync(request);

        // Antiforgery failure is 400; UseStatusCodePagesWithReExecute("/errors/{0}")
        // maps unknown codes including 400 onto the 500 error page.
        Assert.NotEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await FindNoteAsync(_registeredUserId, "module-1", "csrf"));
    }

    private async Task<HttpResponseMessage> SendSaveAsync(
        string method,
        long userId,
        string token,
        Dictionary<string, string> fields)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/my-account/learning-log")
        {
            Content = Form(token, fields),
        };
        request.Headers.Add(TestUserHeader, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> SendAsUserAsync(HttpMethod method, string path, long userId)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(TestUserHeader, userId.ToString());
        return await _client.SendAsync(request);
    }

    private async Task<string> GetTrainingPageAsync(string pageName)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/modules/module-1/content-pages/{pageName}");
        request.Headers.Add(TestUserHeader, _registeredUserId.ToString());
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<List<Event>> FindEventsAsync(long userId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Events
            .Where(e => e.UserId == userId)
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    private static void AssertRailsNoteEvent(Event recorded, string action, string body, string title, string pageName)
    {
        Assert.Equal("/my-account/learning-log", PropertyValue(recorded, "path"));
        Assert.Equal("training/notes", PropertyValue(recorded, "controller"));
        Assert.Equal(action, PropertyValue(recorded, "action"));
        Assert.Equal(body.Length.ToString(CultureInfo.InvariantCulture), PropertyValue(recorded, "length"));
        Assert.Equal(title, PropertyValue(recorded, "title"));
        Assert.Equal("module-1", PropertyValue(recorded, "training_module"));
        Assert.Equal(pageName, PropertyValue(recorded, "name"));
        Assert.DoesNotContain("body", recorded.Properties.Keys);
        Assert.DoesNotContain("module_item_id", recorded.Properties.Keys);
        Assert.DoesNotContain("user", recorded.Properties.Keys);
    }

    private static string PropertyValue(Event recorded, string key) =>
        recorded.Properties[key] is JsonElement element
            ? element.ToString()
            : Convert.ToString(recorded.Properties[key], CultureInfo.InvariantCulture) ?? string.Empty;

    private static FormUrlEncodedContent Form(string token, Dictionary<string, string> fields)
    {
        var payload = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = token,
        };
        return new FormUrlEncodedContent(payload);
    }

    private async Task<string> GetAntiForgeryTokenAsync(long? userId = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        if (userId is not null)
        {
            request.Headers.Add(TestUserHeader, userId.Value.ToString());
        }

        var page = await _client.SendAsync(request);
        page.EnsureSuccessStatusCode();
        return ExtractAntiForgeryToken(await page.Content.ReadAsStringAsync());
    }

    private static string ExtractAntiForgeryToken(string html)
    {
        const string marker = "name=\"__RequestVerificationToken\"";
        var start = html.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new InvalidOperationException("Anti-forgery token not found.");
        }

        var valueStart = html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }

    private async Task<Note?> FindNoteAsync(long userId, string trainingModule, string pageName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Notes
            .AsNoTracking()
            .SingleOrDefaultAsync(n =>
                n.UserId == userId && n.TrainingModule == trainingModule && n.Name == pageName);
    }

    private sealed class LearningLogWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            IntegrationTestHost.Configure(builder);
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

        public async Task<(long RegisteredUserId, long OtherRegisteredUserId, long IncompleteUserId)> SeedUsersAsync()
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var registered = new User
            {
                Email = "learning-log@example.test",
                RegistrationComplete = true,
            };
            var incomplete = new User
            {
                Email = "learning-log-incomplete@example.test",
                RegistrationComplete = false,
            };
            var otherRegistered = new User
            {
                Email = "learning-log-other@example.test",
                RegistrationComplete = true,
            };
            db.Users.AddRange(registered, otherRegistered, incomplete);
            db.UserModuleProgress.Add(new UserModuleProgress
            {
                User = registered,
                ModuleName = "module-1",
                StartedAt = DateTime.UtcNow.AddMinutes(-1),
            });
            await db.SaveChangesAsync();
            return (registered.Id, otherRegistered.Id, incomplete.Id);
        }

        public async Task SeedNoteAsync(long userId, string moduleName, string pageName, string body)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Notes.Add(new Note
            {
                UserId = userId,
                TrainingModule = moduleName,
                Name = pageName,
                Title = "Other note",
                Body = body,
            });
            await db.SaveChangesAsync();
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "LearningLogTest";

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
                    new Claim(AuthConstants.EmailClaim, "learning-log@example.test"),
                    new Claim(ClaimTypes.Name, "Learning Log Learner"),
                ],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
