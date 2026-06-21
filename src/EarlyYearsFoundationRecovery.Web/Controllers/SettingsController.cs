using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Route("settings")]
public class SettingsController(GovUkMarkdownRenderer markdownRenderer) : Controller
{
    public const string AnalyticsCookieName = "track_analytics_v2";

    private const string CookiePolicyBody = """
        # Cookies

        Cookies are small files saved on your phone, tablet or computer when you visit a website.

        We use cookies to make this site work and collect information about how you use our service.

        ## Essential cookies

        Essential cookies keep your information secure while you use Early years child development training. We do not need to ask permission to use them.

        | Name | Purpose | Expires |
        | --- | --- | --- |
        | `.AspNetCore.Cookies` | Stores your session information | Expires when you log out |
        | `track_analytics_v2` | Saves your cookie consent settings | 6 months |

        ## Analytics cookies (optional)

        With your permission, we would use analytics software to collect anonymised data about how you use Early years child development training. Analytics are not enabled in this local development version.

        ## Change your cookie settings
        """;

    [HttpGet("")]
    public IActionResult Show()
    {
        return RedirectToAction(nameof(CookiePolicy));
    }

    [HttpGet("cookie-policy")]
    public IActionResult CookiePolicy()
    {
        var model = new CookiePolicyViewModel
        {
            Body = markdownRenderer.Render(CookiePolicyBody),
            AnalyticsAccepted = Request.Cookies[AnalyticsCookieName] == "true",
            Notice = TempData["Notice"] as string,
        };

        return View(model);
    }

    [HttpPost("")]
    [HttpPost("cookie-policy")]
    [ValidateAntiForgeryToken]
    public IActionResult SaveCookieSettings(CookieSettingsSubmitModel submission)
    {
        var acceptAnalytics = string.Equals(submission.TrackAnalytics, "true", StringComparison.OrdinalIgnoreCase);

        Response.Cookies.Append(
            AnalyticsCookieName,
            acceptAnalytics ? "true" : "false",
            new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddMonths(6),
            });

        if (string.Equals(submission.SettingsUpdated, "true", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Notice"] = "You've set your cookie preferences.";
        }

        var redirectPath = submission.RequestPath;
        if (string.IsNullOrWhiteSpace(redirectPath) || !Url.IsLocalUrl(redirectPath))
        {
            redirectPath = "/settings/cookie-policy";
        }

        return Redirect(redirectPath);
    }
}
