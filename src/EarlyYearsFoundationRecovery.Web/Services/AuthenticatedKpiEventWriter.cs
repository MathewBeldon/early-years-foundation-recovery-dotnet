using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Web.Services;

public sealed class AuthenticatedKpiEventWriter(ApplicationDbContext dbContext, TimeProvider timeProvider)
{
    public async Task TrackAsync(
        HttpContext httpContext,
        long userId,
        string eventName,
        string railsController,
        string railsAction,
        CancellationToken cancellationToken = default,
        IReadOnlyDictionary<string, object?>? extraProperties = null)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var path = httpContext.Request.Path + httpContext.Request.QueryString;
        var visit = await EnsureVisitInternalAsync(httpContext, userId, now, path, cancellationToken);

        var properties = new Dictionary<string, object?>
        {
            ["path"] = path,
            ["controller"] = railsController,
            ["action"] = railsAction,
        };
        if (extraProperties is not null)
        {
            foreach (var pair in extraProperties)
            {
                properties[pair.Key] = pair.Value;
            }
        }

        dbContext.Events.Add(new Event
        {
            VisitId = visit.Id,
            UserId = userId,
            Name = eventName,
            Time = now,
            Properties = properties,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<List<Event>> ListNamedEventsAsync(
        long userId,
        string eventName,
        CancellationToken cancellationToken = default) =>
        dbContext.Events
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.Name == eventName)
            .ToListAsync(cancellationToken);

    public async Task EnsureVisitAsync(
        HttpContext httpContext,
        long userId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var path = httpContext.Request.Path + httpContext.Request.QueryString;
        await EnsureVisitInternalAsync(httpContext, userId, now, path, cancellationToken);
    }

    private async Task<Visit> EnsureVisitInternalAsync(
        HttpContext httpContext,
        long userId,
        DateTime now,
        string path,
        CancellationToken cancellationToken)
    {
        var visitorToken = GetOrSetToken(httpContext, "_ey_visitor", TimeSpan.FromDays(365));
        var visitToken = GetOrSetToken(httpContext, "_ey_visit", TimeSpan.FromMinutes(30));
        var visit = await dbContext.Visits.SingleOrDefaultAsync(x => x.VisitToken == visitToken, cancellationToken);
        if (visit is null)
        {
            visit = new Visit
            {
                VisitToken = visitToken,
                VisitorToken = visitorToken,
                UserId = userId,
                LandingPage = path,
                StartedAt = now,
            };
            dbContext.Visits.Add(visit);
        }
        else if (visit.UserId is null)
        {
            visit.UserId = userId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return visit;
    }

    private static string GetOrSetToken(HttpContext context, string name, TimeSpan lifetime)
    {
        var itemKey = $"{nameof(AuthenticatedKpiEventWriter)}:{name}";
        if (context.Items.TryGetValue(itemKey, out var item) && item is string requestToken)
        {
            return requestToken;
        }

        if (context.Request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            context.Items[itemKey] = value;
            return value;
        }

        var token = Guid.NewGuid().ToString("N");
        context.Items[itemKey] = token;
        context.Response.Cookies.Append(name, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            MaxAge = lifetime,
        });
        return token;
    }
}
