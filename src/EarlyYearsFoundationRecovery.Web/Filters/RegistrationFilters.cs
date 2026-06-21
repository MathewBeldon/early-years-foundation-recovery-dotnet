using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Web.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EarlyYearsFoundationRecovery.Web.Filters;

public sealed class RequireRegistrationIncompleteFilter(IUserRepository users) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new RedirectToActionResult("SignIn", "Account", null);
            return;
        }

        var userId = context.HttpContext.User.GetUserId();
        if (userId is null)
        {
            context.Result = new RedirectToActionResult("SignIn", "Account", null);
            return;
        }

        var user = await users.GetByIdAsync(userId.Value, context.HttpContext.RequestAborted);
        if (user is null)
        {
            context.Result = new RedirectToActionResult("SignIn", "Account", null);
            return;
        }

        if (user.RegistrationComplete)
        {
            context.Result = new RedirectToActionResult("Index", "MyModules", null);
            return;
        }

        await next();
    }
}

public sealed class RequireRegistrationCompleteFilter(
    IUserRepository users,
    IReferenceDataProvider referenceData) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new RedirectToActionResult("SignIn", "Account", null);
            return;
        }

        var userId = context.HttpContext.User.GetUserId();
        if (userId is null)
        {
            context.Result = new RedirectToActionResult("SignIn", "Account", null);
            return;
        }

        var user = await users.GetByIdAsync(userId.Value, context.HttpContext.RequestAborted);
        if (user is null)
        {
            context.Result = new RedirectToActionResult("SignIn", "Account", null);
            return;
        }

        if (!user.RegistrationComplete)
        {
            var step = RegistrationJourney.ResolveCurrentStep(user, referenceData);
            context.Result = new RedirectResult(RegistrationJourney.StepPath(step));
            return;
        }

        await next();
    }
}
