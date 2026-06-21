using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.ViewComponents;

public class SiteNavigationViewComponent(IUserRepository users, IUserModuleProgressRepository progressRepository) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var path = HttpContext.Request.Path.Value ?? "/";
        var isAuthenticated = HttpContext.User.Identity?.IsAuthenticated == true;
        var registrationComplete = false;
        var courseStarted = false;

        if (isAuthenticated)
        {
            var userId = HttpContext.User.GetUserId();
            if (userId is not null)
            {
                var user = await users.GetByIdAsync(userId.Value, HttpContext.RequestAborted);
                registrationComplete = user?.RegistrationComplete == true;

                if (registrationComplete)
                {
                    var progress = await progressRepository.GetForUserAsync(userId.Value, HttpContext.RequestAborted);
                    courseStarted = progress.Any(p => p.StartedAt is not null);
                }
            }
        }

        if (isAuthenticated && !registrationComplete)
        {
            return Content(string.Empty);
        }

        var model = new SiteNavigationViewModel
        {
            IsAuthenticated = isAuthenticated,
            CurrentPath = path,
            ModulesNavActive = path.StartsWith("/about-training", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("/about/", StringComparison.OrdinalIgnoreCase),
            MyModulesNavActive = path.StartsWith("/my-modules", StringComparison.OrdinalIgnoreCase),
            HomeNavActive = path == "/",
            ShowLearningLogNav = isAuthenticated && courseStarted,
            LearningLogNavActive = path.StartsWith("/my-account/learning-log", StringComparison.OrdinalIgnoreCase),
        };

        return View(model);
    }
}
