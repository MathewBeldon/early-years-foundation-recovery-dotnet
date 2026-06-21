using System.Security.Claims;
using EarlyYearsFoundationRecovery.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public static class CookieAuthenticationExtensions
{
    public static AuthenticationBuilder AddAppCookieAuthentication(this IServiceCollection services)
    {
        return services.AddAuthentication(AuthConstants.Scheme)
            .AddCookie(AuthConstants.Scheme, options =>
            {
                options.LoginPath = "/users/sign-in";
                options.LogoutPath = "/users/sign_out";
                options.AccessDeniedPath = "/users/sign-in";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });
    }

    public static ClaimsPrincipal CreatePrincipal(User user)
    {
        var displayName = GetDisplayName(user);
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(AuthConstants.UserIdClaim, user.Id.ToString()),
            new Claim(AuthConstants.EmailClaim, user.Email),
            new Claim(ClaimTypes.Name, displayName),
        ],
        AuthConstants.Scheme));
    }

    public static Task RefreshSignInAsync(HttpContext httpContext, User user) =>
        httpContext.SignInAsync(
            AuthConstants.Scheme,
            CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = true });

    private static string GetDisplayName(User user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(name) ? user.Email : name;
    }
}
