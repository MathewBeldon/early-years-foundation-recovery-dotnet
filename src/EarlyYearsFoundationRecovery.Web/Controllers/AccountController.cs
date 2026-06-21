using System.Security.Cryptography;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Observability;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[AllowAnonymous]
public class AccountController(
    IGovOneAuthService govOneAuth,
    IUserRepository users,
    IReferenceDataProvider referenceData) : Controller
{
    [HttpGet("/account/sign-in")]
    [HttpGet("/users/sign-in")]
    public IActionResult SignIn()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View();
    }

    [HttpGet("/account/login")]
    [HttpGet("/users/auth/openid_connect")]
    public IActionResult Login()
    {
        using var activity = ApplicationTelemetry.StartJourneyActivity("Auth login started", "authentication", "login");
        var state = GenerateSecureToken();
        var nonce = GenerateSecureToken();
        HttpContext.Session.SetString(AuthConstants.GovOneStateSessionKey, state);
        HttpContext.Session.SetString(AuthConstants.GovOneNonceSessionKey, nonce);
        ApplicationTelemetry.RecordAuthEvent("login_started", "succeeded");
        ApplicationTelemetry.MarkActivitySuccess(activity);

        return Redirect(govOneAuth.BuildAuthorizeUrl(state, nonce));
    }

    [HttpGet("/account/callback")]
    [HttpGet("/users/auth/openid_connect/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        CancellationToken cancellationToken)
    {
        using var activity = ApplicationTelemetry.StartJourneyActivity("Auth callback", "authentication", "callback");
        if (!string.IsNullOrWhiteSpace(error))
        {
            ApplicationTelemetry.RecordAuthEvent("callback", "failed", "provider_error");
            ApplicationTelemetry.MarkActivityFailure(activity, "provider_error");
            TempData["ErrorMessage"] = "There was a problem signing in. Please try again.";
            return RedirectToAction(nameof(SignIn));
        }

        var sessionState = HttpContext.Session.GetString(AuthConstants.GovOneStateSessionKey);
        var sessionNonce = HttpContext.Session.GetString(AuthConstants.GovOneNonceSessionKey);
        if (string.IsNullOrWhiteSpace(sessionState) ||
            string.IsNullOrWhiteSpace(sessionNonce) ||
            !string.Equals(state, sessionState, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(code))
        {
            ApplicationTelemetry.RecordAuthEvent("callback", "failed", "invalid_state_or_code");
            ApplicationTelemetry.MarkActivityFailure(activity, "invalid_state_or_code");
            TempData["ErrorMessage"] = "There was a problem signing in. Please try again.";
            return RedirectToAction(nameof(SignIn));
        }

        var tokens = await govOneAuth.ExchangeCodeAsync(code, cancellationToken);
        if (tokens is null)
        {
            ApplicationTelemetry.RecordAuthEvent("callback", "failed", "token_exchange_failed");
            ApplicationTelemetry.MarkActivityFailure(activity, "token_exchange_failed");
            TempData["ErrorMessage"] = "There was a problem signing in. Please try again.";
            return RedirectToAction(nameof(SignIn));
        }

        var idToken = await govOneAuth.ValidateIdTokenAsync(tokens.IdToken, sessionNonce, cancellationToken);
        if (idToken is null)
        {
            ApplicationTelemetry.RecordAuthEvent("callback", "failed", "id_token_validation_failed");
            ApplicationTelemetry.MarkActivityFailure(activity, "id_token_validation_failed");
            TempData["ErrorMessage"] = "There was a problem signing in. Please try again.";
            return RedirectToAction(nameof(SignIn));
        }

        var userInfo = await govOneAuth.GetUserInfoAsync(tokens.AccessToken, cancellationToken);
        if (userInfo is null || !string.Equals(userInfo.Sub, idToken.Sub, StringComparison.Ordinal))
        {
            ApplicationTelemetry.RecordAuthEvent("callback", "failed", "userinfo_validation_failed");
            ApplicationTelemetry.MarkActivityFailure(activity, "userinfo_validation_failed");
            TempData["ErrorMessage"] = "There was a problem signing in. Please try again.";
            return RedirectToAction(nameof(SignIn));
        }

        var user = await users.FindOrCreateFromGovOneAsync(userInfo.Email, userInfo.Sub, cancellationToken);
        HttpContext.Session.SetString(AuthConstants.GovOneIdTokenSessionKey, tokens.IdToken);
        HttpContext.Session.Remove(AuthConstants.GovOneStateSessionKey);
        HttpContext.Session.Remove(AuthConstants.GovOneNonceSessionKey);

        await HttpContext.SignInAsync(
            AuthConstants.Scheme,
            CookieAuthenticationExtensions.CreatePrincipal(user));

        if (user.RegistrationComplete)
        {
            activity?.SetTag("auth.destination", "registered_user");
            ApplicationTelemetry.RecordAuthEvent("callback", "succeeded", "registered_user");
            ApplicationTelemetry.MarkActivitySuccess(activity);
            return Redirect(PostSignInRedirect.ResolveRegisteredUserDestination());
        }

        var step = RegistrationJourney.ResolveCurrentStep(user, referenceData);
        activity?.SetTag("auth.destination", "registration");
        activity?.SetTag("registration.next_step", step);
        ApplicationTelemetry.RecordAuthEvent("callback", "succeeded", "registration_required");
        ApplicationTelemetry.MarkActivitySuccess(activity);
        return Redirect(RegistrationJourney.StepPath(step));
    }

    [HttpGet("/account/sign-out")]
    [HttpGet("/users/sign_out")]
    public async Task<IActionResult> SignOutLocal()
    {
        var idToken = HttpContext.Session.GetString(AuthConstants.GovOneIdTokenSessionKey);
        await HttpContext.SignOutAsync(AuthConstants.Scheme);
        HttpContext.Session.Clear();

        if (!string.IsNullOrWhiteSpace(idToken))
        {
            var state = GenerateSecureToken();
            return Redirect(govOneAuth.BuildLogoutUrl(idToken, state));
        }

        return RedirectToAction("Index", "Home");
    }

    private const string TokenChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    // OIDC state (CSRF) and nonce (replay) are security tokens, so they must come from a CSPRNG.
    private static string GenerateSecureToken() =>
        RandomNumberGenerator.GetString(TokenChars, 32);
}
