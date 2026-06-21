using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.ViewComponents;

public class SiteHeaderViewComponent(IUserRepository users) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var path = HttpContext.Request.Path.Value ?? "/";
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
            ShowSignInLink = !isAuthenticated &&
                !path.StartsWith("/account/sign-in", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/users/sign-in", StringComparison.OrdinalIgnoreCase),
            ShowMyAccountLink = isAuthenticated && registrationComplete,
        };

        return View(model);
    }
}
