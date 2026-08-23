using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

// Contract source: Rails v1.5.0 ac546721, Training::Video,
// training/pages/video_page.html.slim and markup/_video.html.slim.
public sealed class VideoPageHttpTests : IAsyncLifetime
{
    private const string UserHeader = "X-Test-User-Id";
    private VideoPageFactory _factory = null!;
    private HttpClient _client = null!;
    private long _userId;

    public async Task InitializeAsync()
    {
        _factory = new VideoPageFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _userId = await _factory.SeedUserAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData("youtube", "https://www.youtube.com/embed/XnP6jaK7ZAY?enablejsapi=1", "YouTube title")]
    [InlineData("vimeo", "https://player.vimeo.com/video/743243040?enablejsapi=1", "Vimeo title")]
    public async Task Valid_provider_renders_one_titled_Rails_shaped_iframe(
        string page,
        string expectedUrl,
        string expectedTitle)
    {
        var html = await GetHtmlAsync(page);

        Assert.Equal(1, Count(html, "<iframe"));
        Assert.Contains($"title=\"{expectedTitle}\"", html);
        Assert.Contains($"src=\"{WebUtility.HtmlEncode(expectedUrl)}\"", html);
        Assert.Contains("allow=\"accelerometer; autoplay; fullscreen; clipboard-write; encrypted-media; gyroscope; picture-in-picture\"", html);
        Assert.Contains("govuk-details__summary-text\">Transcript", html);
    }

    [Theory]
    [InlineData("invalid-youtube")]
    [InlineData("invalid-vimeo")]
    [InlineData("unknown-provider")]
    [InlineData("missing-id")]
    public async Task Invalid_or_missing_video_fields_render_placeholder_and_never_iframe(string page)
    {
        var html = await GetHtmlAsync(page);

        Assert.DoesNotContain("<iframe", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Video to be added]", html);
    }

    [Fact]
    public async Task Transcript_is_markdown_rendered_and_sanitized()
    {
        var html = await GetHtmlAsync("youtube");
        var transcript = Fragment(html, "<details", "</details>");

        Assert.Contains("<strong>safe transcript</strong>", transcript);
        Assert.DoesNotContain("<script", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert('transcript')", transcript, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", transcript, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Video_get_records_ordinary_progress_without_assessment_or_response_writes()
    {
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("youtube")).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var progress = Assert.Single(await db.UserModuleProgress.AsNoTracking()
            .Where(item => item.UserId == _userId && item.ModuleName == VideoPageFactory.ModuleName)
            .ToListAsync());
        Assert.Equal("youtube", progress.LastPage);
        Assert.Contains("youtube", progress.VisitedPages.Keys);
        Assert.Empty(await db.Assessments.AsNoTracking().Where(item => item.UserId == _userId).ToListAsync());
        Assert.Empty(await db.Responses.AsNoTracking().Where(item => item.UserId == _userId).ToListAsync());
    }

    private async Task<string> GetHtmlAsync(string page)
    {
        var response = await GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> GetAsync(string page)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/modules/{VideoPageFactory.ModuleName}/content-pages/{page}");
        request.Headers.Add(UserHeader, _userId.ToString());
        return await _client.SendAsync(request);
    }

    private static int Count(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

    private static string Fragment(string value, string start, string end)
    {
        var startIndex = value.IndexOf(start, StringComparison.OrdinalIgnoreCase);
        Assert.True(startIndex >= 0, $"Expected fragment start '{start}'.");
        var endIndex = value.IndexOf(end, startIndex, StringComparison.OrdinalIgnoreCase);
        Assert.True(endIndex >= 0, $"Expected fragment end '{end}'.");
        return value[startIndex..(endIndex + end.Length)];
    }

    private sealed class VideoPageFactory : WebApplicationFactory<Program>
    {
        public const string ModuleName = "video-http";
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            IntegrationTestHost.Configure(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(ApplicationDbContext)
                    || descriptor.ServiceType == typeof(DbContextOptions)
                    || descriptor.ServiceType == typeof(DbContextOptions<ApplicationDbContext>)).ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<ITrainingContentProvider>();
                services.AddSingleton<ITrainingContentProvider>(new VideoContentProvider());
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
            db.Users.Add(new User { Email = "video-http@example.test", RegistrationComplete = true });
            await db.SaveChangesAsync();
            return await db.Users.Select(user => user.Id).SingleAsync();
        }

        private sealed class VideoContentProvider : ITrainingContentProvider
        {
            private static readonly TrainingModuleContent Module = new(
                ModuleName,
                "Video module",
                string.Empty,
                string.Empty,
                string.Empty,
                1,
                1,
                true,
                [
                    Video("youtube", "YoUtUbE", "XnP6jaK7ZAY", "YouTube title",
                        "**safe transcript** <script>alert('transcript')</script> [bad](javascript:alert(1))"),
                    Video("vimeo", "VIMEO", "743243040", "Vimeo title", "Vimeo transcript"),
                    Video("invalid-youtube", "youtube", "bad/id/value", "Invalid", "Transcript"),
                    Video("invalid-vimeo", "vimeo", "74324abc", "Invalid", "Transcript"),
                    Video("unknown-provider", "other", "XnP6jaK7ZAY", "Invalid", "Transcript"),
                    Video("missing-id", "youtube", null, "Invalid", "Transcript"),
                ],
                ContentId: "video-http-id");

            public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<TrainingModuleContent>>([Module]);

            public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult<IReadOnlyList<TrainingModuleContent>>([Module]);

            public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default) =>
                Task.FromResult<TrainingModuleContent?>(string.Equals(moduleName, ModuleName, StringComparison.OrdinalIgnoreCase) ? Module : null);

            public Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default) =>
                Task.FromResult<TrainingPageContent?>(
                    string.Equals(moduleName, ModuleName, StringComparison.OrdinalIgnoreCase)
                        ? Module.PageByName(pageName)
                        : null);

            private static TrainingPageContent Video(
                string name,
                string? provider,
                string? id,
                string title,
                string transcript) => new(
                    name,
                    "video_page",
                    $"Heading {name}",
                    "Video body",
                    [],
                    null,
                    null,
                    ContentId: $"{name}-id",
                    VideoProvider: provider,
                    VideoId: id,
                    VideoTitle: title,
                    Transcript: transcript);
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "VideoPageTest";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var values) || !long.TryParse(values, out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [
                    new Claim(AuthConstants.UserIdClaim, userId.ToString()),
                    new Claim(AuthConstants.EmailClaim, "video-http@example.test"),
                ],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
