using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

public class StaticPagesController(
    IStaticContentProvider staticContent,
    IUserRepository users,
    IReferenceDataProvider referenceData,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    [HttpGet("/{pageSlug}")]
    public async Task<IActionResult> Show(string pageSlug, CancellationToken cancellationToken)
    {
        var page = await staticContent.GetPageByNameAsync(pageSlug, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        if (page.RequiresAuth)
        {
            var redirect = await EnsureRegisteredUserAsync(cancellationToken);
            if (redirect is not null)
            {
                return redirect;
            }
        }

        var body = markdownRenderer.Render(page.Body);
        IReadOnlyList<StaticSitemapLinkViewModel>? sitemapLinks = null;

        if (string.Equals(page.Name, "sitemap", StringComparison.OrdinalIgnoreCase))
        {
            sitemapLinks = await BuildSitemapLinksAsync(cancellationToken);
        }

        return View(new StaticPageViewModel
        {
            Title = page.Title,
            Heading = page.Heading,
            Body = body,
            IsSitemap = sitemapLinks is not null,
            SitemapLinks = sitemapLinks ?? [],
        });
    }

    private async Task<IActionResult?> EnsureRegisteredUserAsync(CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return RedirectToAction("SignIn", "Account");
        }

        var userId = User.GetUserId();
        if (userId is null)
        {
            return RedirectToAction("SignIn", "Account");
        }

        var user = await users.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return RedirectToAction("SignIn", "Account");
        }

        if (!user.RegistrationComplete)
        {
            var step = RegistrationJourney.ResolveCurrentStep(user, referenceData);
            return Redirect(RegistrationJourney.StepPath(step));
        }

        return null;
    }

    private async Task<IReadOnlyList<StaticSitemapLinkViewModel>> BuildSitemapLinksAsync(CancellationToken cancellationToken)
    {
        var footerPages = await staticContent.GetFooterPagesAsync(cancellationToken);
        var links = new List<StaticSitemapLinkViewModel>
        {
            new("Home", "/"),
            new("About this training course", "/about-training"),
            new("Give feedback", "/feedback"),
        };

        foreach (var footerPage in footerPages.Where(p => !string.Equals(p.Name, "sitemap", StringComparison.OrdinalIgnoreCase)))
        {
            links.Add(new(footerPage.Heading, $"/{footerPage.Name}"));
        }

        links.Add(new("Cookie policy", "/settings/cookie-policy"));

        if (User.Identity?.IsAuthenticated == true)
        {
            links.Add(new("My modules", "/my-modules"));
            links.Add(new("My account", "/my-account"));
        }

        return links;
    }
}
