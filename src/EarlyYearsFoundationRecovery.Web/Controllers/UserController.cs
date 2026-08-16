using EarlyYearsFoundationRecovery.Application.Feedback;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[TypeFilter(typeof(RequireRegistrationCompleteFilter))]
[Route("my-account")]
public class UserController(
    IUserRepository users,
    IReferenceDataProvider referenceData,
    CourseFeedbackService feedbackService,
    AuthenticatedKpiEventWriter kpiEvents) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Show(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");
        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var model = new MyAccountViewModel
        {
            FullName = UserProfileDisplay.FullName(user),
            CountryName = UserProfileDisplay.CountryName(user),
            SettingName = UserProfileDisplay.SettingName(user, referenceData),
            AuthorityName = UserProfileDisplay.AuthorityName(user),
            RoleName = UserProfileDisplay.RoleName(user, referenceData),
            ExperienceName = UserProfileDisplay.ExperienceName(user, referenceData),
            TrainingEmailsPreference = UserProfileDisplay.TrainingEmailsPreferenceText(user),
            ResearchPreference = UserProfileDisplay.ResearchPreferenceText(user),
            Notice = TempData["Notice"] as string,
            ShowFeedbackCta = !await feedbackService.IsCompleteAsync(userId, cancellationToken),
        };

        // Rails v1.5.0 ac546721 UserController#show: track('profile_page') as the action runs, before View.
        await kpiEvents.TrackAsync(HttpContext, userId, "profile_page", "user", "show", cancellationToken);
        return View(model);
    }
}
