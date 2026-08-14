using System.Security.Claims;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Web.Middleware;

public sealed class AnalyticsCaptureMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
    {
        var eventName = RailsEventName(context.Request.Path);
        if (eventName is null)
        {
            await next(context);
            return;
        }

        var visitorToken = GetOrSetToken(context, "_ey_visitor", TimeSpan.FromDays(365));
        var visitToken = GetOrSetToken(context, "_ey_visit", TimeSpan.FromMinutes(30));
        var userId = long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : (long?)null;
        var visit = await dbContext.Visits.SingleOrDefaultAsync(x => x.VisitToken == visitToken);
        if (visit is null)
        {
            visit = new Visit
            {
                VisitToken = visitToken,
                VisitorToken = visitorToken,
                UserId = userId,
                LandingPage = context.Request.Path + context.Request.QueryString,
                StartedAt = DateTime.UtcNow,
            };
            dbContext.Visits.Add(visit);
            await dbContext.SaveChangesAsync(context.RequestAborted);
        }

        await next(context);
        dbContext.Events.Add(new Event
        {
            VisitId = visit.Id,
            UserId = userId,
            Name = eventName,
            Time = DateTime.UtcNow,
            Properties = new Dictionary<string, object?>
            {
                ["path"] = context.Request.Path.Value,
                ["method"] = context.Request.Method,
                ["status"] = context.Response.StatusCode,
            },
        });
        await dbContext.SaveChangesAsync(context.RequestAborted);
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

    private static string? RailsEventName(PathString path)
    {
        var value = path.Value?.TrimEnd('/') ?? string.Empty;
        return value switch
        {
            "" => "home_page",
            "/about-training" => "course_overview_page",
            "/settings/cookie-policy" => "static_page",
            "/accessibility-statement" or "/terms-and-conditions" or "/sitemap" => "static_page",
            "/feedback" => "feedback_intro",
            "/404" or "/500" or "/503" => "error_page",
            _ => null,
        };
    }
}
