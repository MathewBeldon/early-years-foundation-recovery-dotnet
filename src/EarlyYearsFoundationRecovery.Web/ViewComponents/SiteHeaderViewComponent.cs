using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.ViewComponents;

public class SiteHeaderViewComponent(IUserRepository users) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated == true;
        var registrationComplete = false;

        if (isAuthenticated)
        {
            var userId = HttpContext.User.GetUserId();
            if (userId is not null)
            {
                var user = await users.GetByIdAsync(userId.Value, HttpContext.RequestAborted);
                registrationComplete = user?.RegistrationComplete == true;
            }
        }

        var model = new SiteHeaderViewModel
        {
            IsAuthenticated = isAuthenticated,
            RegistrationComplete = registrationComplete,
            // Rails does not show a second sign-in action in the global header;
            // public pages provide their sign-in call to action in page content.
            ShowSignInLink = false,
            ShowMyAccountLink = isAuthenticated && registrationComplete,
        };

        return View(model);
    }
}
