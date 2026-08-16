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
        CancellationToken cancellationToken = default)
    {
        var visitorToken = GetOrSetToken(httpContext, "_ey_visitor", TimeSpan.FromDays(365));
        var visitToken = GetOrSetToken(httpContext, "_ey_visit", TimeSpan.FromMinutes(30));
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var path = httpContext.Request.Path + httpContext.Request.QueryString;

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

        dbContext.Events.Add(new Event
        {
            VisitId = visit.Id,
            UserId = userId,
            Name = eventName,
            Time = now,
            Properties = new Dictionary<string, object?>
            {
                ["path"] = path,
                ["controller"] = railsController,
                ["action"] = railsAction,
            },
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string GetOrSetToken(HttpContext context, string name, TimeSpan lifetime)
    {
        if (context.Request.Cookies.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var token = Guid.NewGuid().ToString("N");
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
