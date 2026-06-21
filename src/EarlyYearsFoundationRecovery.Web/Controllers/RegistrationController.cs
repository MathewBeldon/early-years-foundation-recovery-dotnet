using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Application.Registration.Commands;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models.Registration;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[Route("registration")]
public class RegistrationController(
    IMediator mediator,
    IUserRepository users,
    IReferenceDataProvider referenceData) : Controller
{
    [HttpGet("terms-and-conditions")]
    [HttpGet("terms-and-conditions/edit")]
    public async Task<IActionResult> TermsAndConditions(CancellationToken cancellationToken)
    {
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.TermsAndConditions, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new TermsAndConditionsViewModel { Accepted = false });
    }

    [HttpPost("terms-and-conditions")]
    [HttpPost("terms-and-conditions/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.TermsAndConditions)]
    public async Task<IActionResult> TermsAndConditions(TermsAndConditionsViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateTermsAndConditionsCommand(GetUserId(), model.Accepted),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            return View(model);
        }
    }

    [HttpGet("name")]
    [HttpGet("name/edit")]
    public async Task<IActionResult> Name(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.Name, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new NameViewModel
        {
            FirstName = user.FirstName ?? string.Empty,
            LastName = user.LastName ?? string.Empty,
        });
    }

    [HttpPost("name")]
    [HttpPost("name/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.Name)]
    public async Task<IActionResult> Name(NameViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateNameCommand(GetUserId(), model.FirstName, model.LastName),
                cancellationToken);
            await RefreshSignInAsync(cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            foreach (var error in ex.Errors)
            {
                ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
            }

            return View(model);
        }
    }

    [HttpGet("where-you-live")]
    [HttpGet("where-you-live/edit")]
    public async Task<IActionResult> WhereYouLive(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.WhereYouLive, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        var currentCountry = referenceData.Countries
            .FirstOrDefault(option => option.Label == user.Country)?.Id ?? string.Empty;

        return View(new WhereYouLiveViewModel
        {
            CountryId = currentCountry,
            Options = referenceData.Countries,
        });
    }

    [HttpPost("where-you-live")]
    [HttpPost("where-you-live/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.WhereYouLive)]
    public async Task<IActionResult> WhereYouLive(WhereYouLiveViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateWhereYouLiveCommand(GetUserId(), model.CountryId),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            model.Options = referenceData.Countries;
            return View(model);
        }
    }

    [HttpGet("setting-type")]
    [HttpGet("setting-type/edit")]
    public async Task<IActionResult> SettingType(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.SettingType, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new SettingTypeViewModel
        {
            SettingTypeId = user.SettingType ?? string.Empty,
            Options = referenceData.SettingTypes,
        });
    }

    [HttpPost("setting-type")]
    [HttpPost("setting-type/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.SettingType)]
    public async Task<IActionResult> SettingType(SettingTypeViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateSettingTypeCommand(GetUserId(), model.SettingTypeId),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            model.Options = referenceData.SettingTypes;
            return View(model);
        }
    }

    [HttpGet("setting-type-other")]
    [HttpGet("setting-type-other/edit")]
    public async Task<IActionResult> SettingTypeOther(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.SettingTypeOther, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new SettingTypeOtherViewModel
        {
            SettingTypeOther = user.SettingTypeOther ?? string.Empty,
        });
    }

    [HttpPost("setting-type-other")]
    [HttpPost("setting-type-other/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.SettingTypeOther)]
    public async Task<IActionResult> SettingTypeOther(SettingTypeOtherViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateSettingTypeOtherCommand(GetUserId(), model.SettingTypeOther),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            return View(model);
        }
    }

    [HttpGet("local-authority")]
    [HttpGet("local-authority/edit")]
    public async Task<IActionResult> LocalAuthority(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.LocalAuthority, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        var currentAuthority = referenceData.LocalAuthorities
            .FirstOrDefault(option => option.Label == user.LocalAuthority)?.Id ?? string.Empty;

        return View(new LocalAuthorityViewModel
        {
            LocalAuthorityId = currentAuthority,
            Options = referenceData.LocalAuthorities,
        });
    }

    [HttpPost("local-authority")]
    [HttpPost("local-authority/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.LocalAuthority)]
    public async Task<IActionResult> LocalAuthority(LocalAuthorityViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateLocalAuthorityCommand(GetUserId(), model.LocalAuthorityId, model.Skip),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            model.Options = referenceData.LocalAuthorities;
            return View(model);
        }
    }

    [HttpGet("role-type")]
    [HttpGet("role-type/edit")]
    public async Task<IActionResult> RoleType(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.RoleType, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        var roleGroup = RegistrationJourney.RoleGroupFor(user, referenceData);
        return View(new RoleTypeViewModel
        {
            RoleTypeId = user.RoleType ?? string.Empty,
            Options = referenceData.GetRolesForGroup(roleGroup),
        });
    }

    [HttpPost("role-type")]
    [HttpPost("role-type/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.RoleType)]
    public async Task<IActionResult> RoleType(RoleTypeViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateRoleTypeCommand(GetUserId(), model.RoleTypeId),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            var user = await GetUserAsync(cancellationToken);
            model.Options = referenceData.GetRolesForGroup(RegistrationJourney.RoleGroupFor(user, referenceData));
            return View(model);
        }
    }

    [HttpGet("role-type-other")]
    [HttpGet("role-type-other/edit")]
    public async Task<IActionResult> RoleTypeOther(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.RoleTypeOther, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new RoleTypeOtherViewModel
        {
            RoleTypeOther = user.RoleTypeOther ?? string.Empty,
        });
    }

    [HttpPost("role-type-other")]
    [HttpPost("role-type-other/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.RoleTypeOther)]
    public async Task<IActionResult> RoleTypeOther(RoleTypeOtherViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateRoleTypeOtherCommand(GetUserId(), model.RoleTypeOther),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            return View(model);
        }
    }

    [HttpGet("early-years-experience")]
    [HttpGet("early-years-experience/edit")]
    public async Task<IActionResult> EarlyYearsExperience(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.EarlyYearsExperience, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new EarlyYearsExperienceViewModel
        {
            ExperienceId = user.EarlyYearsExperience ?? string.Empty,
            Options = referenceData.ExperienceLevels.Where(x => x.Id != "na").ToList(),
        });
    }

    [HttpPost("early-years-experience")]
    [HttpPost("early-years-experience/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.EarlyYearsExperience)]
    public async Task<IActionResult> EarlyYearsExperience(EarlyYearsExperienceViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateEarlyYearsExperienceCommand(GetUserId(), model.ExperienceId),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            model.Options = referenceData.ExperienceLevels.Where(x => x.Id != "na").ToList();
            return View(model);
        }
    }

    [HttpGet("training-emails")]
    [HttpGet("training-emails/edit")]
    public async Task<IActionResult> TrainingEmails(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.TrainingEmails, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new TrainingEmailsViewModel
        {
            TrainingEmails = user.TrainingEmails,
        });
    }

    [HttpPost("training-emails")]
    [HttpPost("training-emails/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.TrainingEmails)]
    public async Task<IActionResult> TrainingEmails(TrainingEmailsViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateTrainingEmailsCommand(GetUserId(), model.TrainingEmails),
                cancellationToken);
            await RefreshSignInAsync(cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            return View(model);
        }
    }

    [HttpGet("research-participant")]
    [HttpGet("research-participant/edit")]
    public async Task<IActionResult> ResearchParticipant(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.ResearchParticipant, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(new ResearchParticipantViewModel
        {
            ResearchParticipant = user.ResearchParticipant,
        });
    }

    [HttpPost("research-participant")]
    [HttpPost("research-participant/edit")]
    [ValidateAntiForgeryToken]
    [RegistrationStepTelemetry(RegistrationJourney.ResearchParticipant)]
    public async Task<IActionResult> ResearchParticipant(ResearchParticipantViewModel model, CancellationToken cancellationToken)
    {
        try
        {
            var nextUrl = await mediator.Send(
                new UpdateResearchParticipantCommand(GetUserId(), model.ResearchParticipant),
                cancellationToken);
            return await RedirectAfterRegistrationStepAsync(nextUrl, cancellationToken);
        }
        catch (ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Errors.First().ErrorMessage);
            return View(model);
        }
    }

    [HttpGet("check-your-answers")]
    [HttpGet("check-your-answers/edit")]
    public async Task<IActionResult> CheckYourAnswers(CancellationToken cancellationToken)
    {
        // Returning to the summary ends any in-progress review of answers.
        EndReviewMode();
        var user = await GetUserAsync(cancellationToken);
        var redirect = await EnsureCurrentStepAsync(RegistrationJourney.CheckYourAnswers, cancellationToken);
        if (redirect is not null)
        {
            return redirect;
        }

        return View(BuildCheckYourAnswersViewModel(user));
    }

    [HttpPost("check-your-answers")]
    [HttpPost("check-your-answers/edit")]
    [ValidateAntiForgeryToken]
    [ActionName("CheckYourAnswers")]
    [RegistrationStepTelemetry(RegistrationJourney.CheckYourAnswers)]
    public async Task<IActionResult> CheckYourAnswersPost(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user.RegistrationComplete)
        {
            TempData["Notice"] = "You have updated your details";
            return Redirect("/my-account");
        }

        var nextUrl = await mediator.Send(new CompleteRegistrationCommand(GetUserId()), cancellationToken);
        await RefreshSignInAsync(cancellationToken);
        TempData["Notice"] = "Thank you for creating an Early years child development training account. You can now start your first module.";
        return Redirect(nextUrl);
    }

    private CheckYourAnswersViewModel BuildCheckYourAnswersViewModel(Domain.Entities.User user)
    {
        var settingType = referenceData.GetSettingType(user.SettingType);
        return new CheckYourAnswersViewModel
        {
            FullName = UserProfileDisplay.FullName(user),
            CountryName = UserProfileDisplay.CountryName(user),
            SettingName = UserProfileDisplay.SettingName(user, referenceData),
            AuthorityName = UserProfileDisplay.AuthorityName(user),
            RoleName = UserProfileDisplay.RoleName(user, referenceData),
            ExperienceName = UserProfileDisplay.ExperienceName(user, referenceData),
            ShowAuthority = settingType is not null &&
                RegistrationJourney.IsEngland(user) &&
                settingType.RequiresLocalAuthority &&
                !RegistrationJourney.IsNotApplicable(user.LocalAuthority),
            ShowRole = settingType is not null &&
                RegistrationJourney.RequiresRoleStep(user, settingType) &&
                !RegistrationJourney.IsNotApplicable(user.RoleType),
            ShowExperience = UserProfileDisplay.ShowsExperience(user, referenceData),
            TrainingEmailsPreference = UserProfileDisplay.TrainingEmailsPreferenceText(user),
            ResearchPreference = UserProfileDisplay.ResearchPreferenceText(user),
        };
    }

    private const string ReviewFlagKey = "registration_review";

    private static readonly HashSet<string> ProfileEditChainSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        RegistrationJourney.StepPath(RegistrationJourney.WhereYouLive),
        RegistrationJourney.StepPath(RegistrationJourney.SettingTypeOther),
        RegistrationJourney.StepPath(RegistrationJourney.LocalAuthority),
        RegistrationJourney.StepPath(RegistrationJourney.RoleType),
        RegistrationJourney.StepPath(RegistrationJourney.RoleTypeOther),
        RegistrationJourney.StepPath(RegistrationJourney.EarlyYearsExperience),
    };

    private async Task<IActionResult> RedirectAfterRegistrationStepAsync(string nextUrl, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);

        if (!user.RegistrationComplete)
        {
            // When editing a single answer from "Check your answers", return the
            // user to the summary (or the next still-incomplete step) rather than
            // continuing the linear journey.
            if (IsReviewing(user))
            {
                return Redirect(ResumeRegistrationPath(user));
            }

            return Redirect(nextUrl);
        }

        if (ProfileEditChainSteps.Contains(nextUrl))
        {
            return Redirect(nextUrl);
        }

        TempData["Notice"] = "You have updated your details";
        return Redirect("/my-account");
    }

    private bool IsReviewing(Domain.Entities.User user) =>
        !user.RegistrationComplete &&
        string.Equals(HttpContext.Session.GetString(ReviewFlagKey), "1", StringComparison.Ordinal);

    private void EndReviewMode() => HttpContext.Session.Remove(ReviewFlagKey);

    private string ResumeRegistrationPath(Domain.Entities.User user) =>
        RegistrationJourney.StepPath(RegistrationJourney.ResolveCurrentStep(user, referenceData));

    private long GetUserId() => User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");

    private async Task<Domain.Entities.User> GetUserAsync(CancellationToken cancellationToken) =>
        await users.GetByIdAsync(GetUserId(), cancellationToken)
        ?? throw new InvalidOperationException("User not found.");

    private async Task<IActionResult?> EnsureCurrentStepAsync(string requestedStep, CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user.RegistrationComplete)
        {
            ViewData["BackLink"] = ComputeBackLink(user, requestedStep);
            return null;
        }

        // Arriving at an edit page from "Check your answers" starts review mode,
        // letting the user change one answer and be sent back to the summary.
        if (string.Equals(Request.Query["return_to"], RegistrationJourney.CheckYourAnswers, StringComparison.Ordinal))
        {
            HttpContext.Session.SetString(ReviewFlagKey, "1");
        }

        // While reviewing, allow an already-answered step to be edited directly
        // instead of bouncing back to the next incomplete step.
        if (IsReviewing(user))
        {
            ViewData["BackLink"] = ComputeBackLink(user, requestedStep);
            return null;
        }

        var currentStep = RegistrationJourney.ResolveCurrentStep(user, referenceData);
        if (!string.Equals(currentStep, requestedStep, StringComparison.Ordinal))
        {
            return Redirect(RegistrationJourney.StepPath(currentStep));
        }

        ViewData["BackLink"] = ComputeBackLink(user, requestedStep);
        return null;
    }

    private string? ComputeBackLink(Domain.Entities.User user, string step)
    {
        if (user.RegistrationComplete)
        {
            return "/my-account";
        }

        if (IsReviewing(user))
        {
            return RegistrationJourney.StepPath(RegistrationJourney.CheckYourAnswers);
        }

        var previous = RegistrationJourney.PreviousVisibleStep(user, step, referenceData);
        return previous is null ? null : RegistrationJourney.StepPath(previous);
    }

    private async Task RefreshSignInAsync(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        await CookieAuthenticationExtensions.RefreshSignInAsync(HttpContext, user);
    }
}
