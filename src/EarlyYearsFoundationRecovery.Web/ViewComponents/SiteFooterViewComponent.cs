using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.ViewComponents;

public class SiteFooterViewComponent(IStaticContentProvider staticContent) : ViewComponent
{
    private const string PrivacyPolicyUrl =
        "https://www.gov.uk/government/publications/privacy-information-members-of-the-public/privacy-information-members-of-the-public";

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var footerPages = await staticContent.GetFooterPagesAsync(HttpContext.RequestAborted);

        var model = new SiteFooterViewModel
        {
            FooterPages = footerPages
                .Select(page => new SiteFooterLinkViewModel(page.Heading, $"/{page.Name}"))
                .ToList(),
            PrivacyPolicyUrl = PrivacyPolicyUrl,
        };

        return View(model);
    }
}
