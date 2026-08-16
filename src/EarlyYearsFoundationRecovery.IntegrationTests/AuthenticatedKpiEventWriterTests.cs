using System.Text.Json;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public class AuthenticatedKpiEventWriterTests
{
    // Rails v1.5.0 ac546721 app/controllers/concerns/tracking.rb Tracking#track,
    // app/controllers/learning_controller.rb LearningController#show,
    // and app/controllers/user_controller.rb UserController#show.
    [Fact]
    public async Task Two_cookie_bearing_requests_share_one_visit_and_write_learning_then_profile_events()
    {
        await using var db = CreateDb();
        var writer = new AuthenticatedKpiEventWriter(db, TimeProvider.System);
        const long userId = 42;

        var learning = CreateHttpContext("/my-modules", "?from=nav");
        await writer.TrackAsync(learning, userId, "learning_page", "learning", "show");

        var cookies = CookieHeaderFromResponse(learning);
        var visitToken = ReadCookie(learning, "_ey_visit");

        var profile = CreateHttpContext("/my-account", cookieHeader: cookies);
        await writer.TrackAsync(profile, userId, "profile_page", "user", "show");

        var visits = await db.Visits.OrderBy(v => v.Id).ToListAsync();
        var events = await db.Events.OrderBy(e => e.Id).ToListAsync();

        var visit = Assert.Single(visits);
        Assert.Equal(visitToken, visit.VisitToken);
        Assert.Equal(userId, visit.UserId);
        Assert.Equal("/my-modules?from=nav", visit.LandingPage);
        Assert.Equal(2, events.Count);
        Assert.All(events, e =>
        {
            Assert.Equal(visit.Id, e.VisitId);
            Assert.Equal(userId, e.UserId);
        });

        Assert.Equal("learning_page", events[0].Name);
        AssertRailsProperties(events[0].Properties, "/my-modules?from=nav", "learning", "show");
        Assert.Equal("profile_page", events[1].Name);
        AssertRailsProperties(events[1].Properties, "/my-account", "user", "show");
        Assert.Equal(visitToken, ReadRequestCookie(profile, "_ey_visit"));
        Assert.DoesNotContain(
            profile.Response.Headers.SetCookie,
            header => header is not null && header.StartsWith("_ey_visit=", StringComparison.Ordinal));
    }

    // Rails v1.5.0 ac546721 Tracking#track plus Ahoy DatabaseStore#authenticate:
    // attach when visit.user is nil; do not overwrite a non-null user.
    [Fact]
    public async Task Attaches_anonymous_visit_to_authenticated_user_without_overwriting_a_different_user()
    {
        await using var db = CreateDb();
        var writer = new AuthenticatedKpiEventWriter(db, TimeProvider.System);
        var authenticated = new User { Email = "learner@example.test", RegistrationComplete = true };
        var other = new User { Email = "other@example.test", RegistrationComplete = true };
        db.Users.AddRange(authenticated, other);
        await db.SaveChangesAsync();

        var anonymousToken = Guid.NewGuid().ToString("N");
        var ownedToken = Guid.NewGuid().ToString("N");
        db.Visits.AddRange(
            new Visit
            {
                VisitToken = anonymousToken,
                VisitorToken = Guid.NewGuid().ToString("N"),
                UserId = null,
                LandingPage = "/about-training",
                StartedAt = DateTime.UtcNow.AddMinutes(-5),
            },
            new Visit
            {
                VisitToken = ownedToken,
                VisitorToken = Guid.NewGuid().ToString("N"),
                UserId = other.Id,
                LandingPage = "/feedback",
                StartedAt = DateTime.UtcNow.AddMinutes(-4),
            });
        await db.SaveChangesAsync();

        var attach = CreateHttpContext("/my-modules", cookieHeader: $"_ey_visit={anonymousToken}");
        await writer.TrackAsync(attach, authenticated.Id, "learning_page", "learning", "show");

        var keepOwner = CreateHttpContext("/my-account", cookieHeader: $"_ey_visit={ownedToken}");
        await writer.TrackAsync(keepOwner, authenticated.Id, "profile_page", "user", "show");

        var anonymousVisit = await db.Visits.SingleAsync(v => v.VisitToken == anonymousToken);
        var ownedVisit = await db.Visits.SingleAsync(v => v.VisitToken == ownedToken);
        Assert.Equal(authenticated.Id, anonymousVisit.UserId);
        Assert.Equal(other.Id, ownedVisit.UserId);

        var events = await db.Events.OrderBy(e => e.Id).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.Equal(anonymousVisit.Id, events[0].VisitId);
        Assert.Equal(authenticated.Id, events[0].UserId);
        Assert.Equal(ownedVisit.Id, events[1].VisitId);
        Assert.Equal(authenticated.Id, events[1].UserId);
        Assert.Equal(2, await db.Visits.CountAsync());
    }

    [Fact]
    public async Task Ensure_visit_records_authenticated_registration_without_creating_an_event()
    {
        await using var db = CreateDb();
        var writer = new AuthenticatedKpiEventWriter(db, TimeProvider.System);
        var context = CreateHttpContext("/registration/research-participant/edit");

        await writer.EnsureVisitAsync(context, 42);
        await writer.EnsureVisitAsync(context, 42);

        var visit = Assert.Single(await db.Visits.ToListAsync());
        Assert.Equal(42, visit.UserId);
        Assert.Equal("/registration/research-participant/edit", visit.LandingPage);
        Assert.Empty(await db.Events.ToListAsync());
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DefaultHttpContext CreateHttpContext(
        string path,
        string? queryString = null,
        string? cookieHeader = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (queryString is not null)
        {
            context.Request.QueryString = new QueryString(queryString);
        }

        if (cookieHeader is not null)
        {
            context.Request.Headers.Cookie = cookieHeader;
        }

        return context;
    }

    private static string CookieHeaderFromResponse(HttpContext context) =>
        string.Join("; ", SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie)
            .Select(cookie => $"{cookie.Name}={cookie.Value}"));

    private static string ReadCookie(HttpContext context, string name)
    {
        var cookie = SetCookieHeaderValue.ParseList(context.Response.Headers.SetCookie)
            .Single(value => value.Name == name);
        return cookie.Value.ToString();
    }

    private static string ReadRequestCookie(HttpContext context, string name)
    {
        Assert.True(context.Request.Cookies.TryGetValue(name, out var value));
        Assert.False(string.IsNullOrWhiteSpace(value));
        return value;
    }

    private static void AssertRailsProperties(
        Dictionary<string, object?> properties,
        string path,
        string controller,
        string action)
    {
        Assert.Equal(3, properties.Count);
        Assert.Equal(path, PropertyString(properties, "path"));
        Assert.Equal(controller, PropertyString(properties, "controller"));
        Assert.Equal(action, PropertyString(properties, "action"));
        Assert.DoesNotContain("method", properties.Keys);
        Assert.DoesNotContain("status", properties.Keys);
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
}
