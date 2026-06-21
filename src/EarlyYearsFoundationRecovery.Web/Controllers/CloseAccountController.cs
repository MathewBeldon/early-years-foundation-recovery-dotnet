using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Observability;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Route("my-account/close")]
public class CloseAccountController(ICloseAccountService closeAccountService) : Controller
{
    [Authorize]
    [TypeFilter(typeof(RequireRegistrationCompleteFilter))]
    [HttpGet("edit-reason")]
    public IActionResult EditReason()
    {
        return View(new CloseAccountEditReasonViewModel());
    }

    [Authorize]
    [TypeFilter(typeof(RequireRegistrationCompleteFilter))]
    [HttpPost("update-reason")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReason(CloseAccountEditReasonViewModel model, CancellationToken cancellationToken)
    {
        using var activity = ApplicationTelemetry.StartJourneyActivity(
            "Account closure reason saved",
            "account_closure",
            "reason");
        try
        {
            await closeAccountService.SaveCloseReasonAsync(
                GetUserId(),
                model.ClosedReason ?? string.Empty,
                model.ClosedReasonCustom,
                cancellationToken);

            ApplicationTelemetry.RecordAccountClosureEvent("reason_saved", "succeeded");
            ApplicationTelemetry.MarkActivitySuccess(activity);
            return RedirectToAction(nameof(Confirm));
        }
        catch (ArgumentException ex)
        {
            ApplicationTelemetry.RecordAccountClosureEvent("reason_saved", "failed", "validation_failed");
            ApplicationTelemetry.MarkActivityFailure(activity, "validation_failed");
            ModelState.AddModelError(nameof(CloseAccountEditReasonViewModel.ClosedReason), ex.Message);
            return View(nameof(EditReason), model);
        }
    }

    [Authorize]
    [TypeFilter(typeof(RequireRegistrationCompleteFilter))]
    [HttpGet("confirm")]
    public async Task<IActionResult> Confirm(CancellationToken cancellationToken)
    {
        var user = await GetRequiredUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(user.ClosedReason))
        {
            return RedirectToAction(nameof(EditReason));
        }

        return View();
    }

    [Authorize]
    [TypeFilter(typeof(RequireRegistrationCompleteFilter))]
    [HttpPost("close-account")]
    [HttpPost("close_account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseAccount(CancellationToken cancellationToken)
    {
        using var activity = ApplicationTelemetry.StartJourneyActivity(
            "Account closed",
            "account_closure",
            "close");
        var user = await GetRequiredUserAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(user.ClosedReason))
        {
            ApplicationTelemetry.RecordAccountClosureEvent("account_closed", "failed", "missing_reason");
            ApplicationTelemetry.MarkActivityFailure(activity, "missing_reason");
            return RedirectToAction(nameof(EditReason));
        }

        await closeAccountService.RedactAndCloseAsync(user.Id, cancellationToken);
        await HttpContext.SignOutAsync(AuthConstants.Scheme);
        HttpContext.Session.Clear();

        ApplicationTelemetry.RecordAccountClosureEvent("account_closed", "succeeded");
        ApplicationTelemetry.MarkActivitySuccess(activity);
        return RedirectToAction(nameof(Show));
    }

    [AllowAnonymous]
    [HttpGet("")]
    public IActionResult Show()
    {
        return View();
    }

    private long GetUserId() =>
        User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");

    private async Task<Domain.Entities.User> GetRequiredUserAsync(CancellationToken cancellationToken)
    {
        var userRepository = HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        return await userRepository.GetByIdAsync(GetUserId(), cancellationToken)
            ?? throw new InvalidOperationException("User not found.");
    }
}
