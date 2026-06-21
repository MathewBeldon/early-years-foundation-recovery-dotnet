using EarlyYearsFoundationRecovery.Application.Observability;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EarlyYearsFoundationRecovery.Web.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RegistrationStepTelemetryAttribute(string step) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        using var activity = ApplicationTelemetry.StartJourneyActivity(
            "Registration step submitted",
            "registration",
            step);
        activity?.SetTag("registration.step", step);

        var executed = await next();
        var result = ResolveResult(context, executed);
        var mode = ResolveMode(context);
        var nextStep = ResolveNextStep(executed.Result);

        activity?.SetTag("registration.result", result);
        activity?.SetTag("registration.mode", mode);
        if (!string.IsNullOrWhiteSpace(nextStep))
        {
            activity?.SetTag("registration.next_step", nextStep);
        }

        if (result == "exception")
        {
            ApplicationTelemetry.MarkActivityFailure(activity, result);
        }
        else
        {
            ApplicationTelemetry.MarkActivitySuccess(activity);
        }

        ApplicationTelemetry.RecordRegistrationStep(step, result, mode, nextStep);
    }

    private static string ResolveResult(ActionExecutingContext context, ActionExecutedContext executed)
    {
        if (executed.Exception is not null && !executed.ExceptionHandled)
        {
            return "exception";
        }

        return context.ModelState.IsValid ? "completed" : "validation_failed";
    }

    private static string ResolveMode(ActionContext context)
    {
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        return path.EndsWith("/edit", StringComparison.OrdinalIgnoreCase) ? "edit" : "initial";
    }

    private static string? ResolveNextStep(IActionResult? result)
    {
        var url = result switch
        {
            RedirectResult redirect => redirect.Url,
            RedirectToActionResult redirectToAction => redirectToAction.ActionName,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (url.StartsWith("/registration/", StringComparison.OrdinalIgnoreCase))
        {
            return url["/registration/".Length..].Split('?', StringSplitOptions.RemoveEmptyEntries)[0];
        }

        return url.StartsWith("/", StringComparison.Ordinal) ? url.Trim('/') : url;
    }
}
