using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

public sealed class CertificateHttpTests : IAsyncLifetime
{
    private const string UserHeader = "X-Test-User-Id";
    private const string ModuleName = "module-2";
    private const string CertificatePath = "/modules/module-2/content-pages/certificate";
    private const string PdfPath = "/modules/module-2/content-pages/certificate.pdf";

    private CertificateWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private long _completeUserId;
    private long _incompleteUserId;

    public async Task InitializeAsync()
    {
        _factory = new CertificateWebApplicationFactory();
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        _completeUserId = await _factory.SeedUserAsync(
            "certificate-complete@example.test",
            "Certificate",
            "Complete",
            completed: true,
            passed: true);
        _incompleteUserId = await _factory.SeedUserAsync(
            "certificate-incomplete@example.test",
            "Certificate",
            "Incomplete",
            completed: false,
            passed: false);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Completed_certificate_html_has_rails_content_and_canonical_pdf_link()
    {
        var response = await GetAsUserAsync(CertificatePath, _completeUserId);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Get your certificate", body, StringComparison.Ordinal);
        Assert.Contains("Certificate Complete", body, StringComparison.Ordinal);
        Assert.Contains("Date completed: 5 January 2026", body, StringComparison.Ordinal);
        Assert.Contains("Dummy criteria 1", body, StringComparison.Ordinal);
        Assert.Contains(PdfPath, body, StringComparison.Ordinal);
        Assert.DoesNotContain("stub", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Incomplete_certificate_html_and_pdf_are_placeholders_and_preserve_state()
    {
        var html = await GetAsUserAsync(CertificatePath, _incompleteUserId);
        var htmlBody = await html.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        Assert.Contains("You have not yet completed the module.", htmlBody, StringComparison.Ordinal);
        Assert.Contains("Your name will appear here", htmlBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Certificate Incomplete", htmlBody, StringComparison.Ordinal);
        Assert.Contains(PdfPath, htmlBody, StringComparison.Ordinal);

        var pdf = await GetAsUserAsync(PdfPath, _incompleteUserId);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        var text = System.Text.Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 5));

        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Null(pdf.Content.Headers.ContentDisposition);
        Assert.Equal("%PDF-", text);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var progress = await db.UserModuleProgress.SingleAsync(x => x.UserId == _incompleteUserId && x.ModuleName == ModuleName);
        var assessment = await db.Assessments.SingleAsync(x => x.UserId == _incompleteUserId && x.TrainingModule == ModuleName);

        Assert.Null(progress.CompletedAt);
        Assert.Equal("certificate", progress.LastPage);
        Assert.Contains("certificate", progress.VisitedPages.Keys);
        Assert.Equal(50, assessment.Score);
        Assert.False(assessment.Passed);
    }

    [Fact]
    public async Task Compatibility_certificate_pdf_alias_remains_available_without_attachment_disposition()
    {
        var response = await GetAsUserAsync("/modules/module-2/certificate.pdf", _completeUserId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Null(response.Content.Headers.ContentDisposition);
    }

    private async Task<HttpResponseMessage> GetAsUserAsync(string path, long userId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(UserHeader, userId.ToString());
        return await _client.SendAsync(request);
    }

    private sealed class CertificateWebApplicationFactory : WebApplicationFactory<Program>
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

        public async Task<long> SeedUserAsync(
            string email,
            string firstName,
            string lastName,
            bool completed,
            bool passed)
        {
            await using var scope = Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = new User
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                RegistrationComplete = true,
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var visited = new Dictionary<string, string>
            {
                ["what-to-expect"] = "2026-01-05T00:00:00Z",
                ["assessment-results"] = "2026-01-05T00:09:00Z",
            };
            if (completed)
            {
                visited["certificate"] = "2026-01-05T00:10:00Z";
            }

            db.Assessments.Add(new Assessment
            {
                UserId = user.Id,
                TrainingModule = ModuleName,
                Score = passed ? 75 : 50,
                Passed = passed,
                StartedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                CompletedAt = new DateTime(2026, 1, 5, 0, 30, 0, DateTimeKind.Utc),
            });
            db.UserModuleProgress.Add(new UserModuleProgress
            {
                UserId = user.Id,
                ModuleName = ModuleName,
                StartedAt = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                CompletedAt = completed ? new DateTime(2026, 1, 5, 0, 30, 0, DateTimeKind.Utc) : null,
                LastPage = completed ? "certificate" : "assessment-results",
                VisitedPages = visited,
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
        public const string SchemeName = "CertificateTest";

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
                    new Claim(AuthConstants.EmailClaim, "certificate@example.test"),
                    new Claim(ClaimTypes.Name, "Certificate Learner"),
                ],
                SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
