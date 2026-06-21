using EarlyYearsFoundationRecovery.Web.Controllers;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.ViewComponents;

public class CookieBannerViewComponent : ViewComponent
{
    public IViewComponentResult Invoke()
    {
        if (HttpContext.Request.Cookies.ContainsKey(SettingsController.AnalyticsCookieName))
        {
            return Content(string.Empty);
        }

        var model = new CookieBannerViewModel
        {
            RequestPath = HttpContext.Request.Path.Value ?? "/",
        };

        return View(model);
    }
}
