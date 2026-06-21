using System.Security.Claims;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public static class ClaimsPrincipalExtensions
{
    public static long? GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(AuthConstants.UserIdClaim);
        return long.TryParse(value, out var userId) ? userId : null;
    }

    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(AuthConstants.EmailClaim);
}
