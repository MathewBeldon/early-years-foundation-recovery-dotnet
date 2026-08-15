using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public static class BotAuthenticationControllerExtensions
{
    public static IActionResult? EnforceBotAuthentication(
        this ControllerBase controller,
        BotAuthenticationFailureTracker tracker,
        string scope,
        bool isValid)
    {
        var clientIp = controller.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (isValid)
        {
            tracker.AuthenticationSucceeded(scope, clientIp);
            return null;
        }

        if (tracker.RegisterFailure(scope, clientIp))
        {
            return controller.StatusCode(
                StatusCodes.Status429TooManyRequests,
                new { status = "rate limited" });
        }

        return controller.Unauthorized(new { status = "invalid secure header" });
    }
}
