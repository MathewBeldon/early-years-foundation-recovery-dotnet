using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EarlyYearsFoundationRecovery.Infrastructure.Services;
using EarlyYearsFoundationRecovery.Web.Authentication;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[ApiController]
[Route("notify")]
public class NotifyController(
    INotifyCallbackHandler notifyCallbackHandler,
    IOptions<NotifyOptions> options,
    BotAuthenticationFailureTracker failureTracker) : ControllerBase
{
    private const string AuthenticationScope = "notify-webhook";

    [HttpPost]
    public async Task<IActionResult> Update(CancellationToken cancellationToken)
    {
        var token = BearerToken(Request.Headers.Authorization.FirstOrDefault());
        var authenticationFailure = this.EnforceBotAuthentication(
            failureTracker,
            AuthenticationScope,
            BotAuthentication.SecretsMatch(token, options.Value.CallbackToken));
        if (authenticationFailure is not null)
        {
            return authenticationFailure;
        }
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var matched = await notifyCallbackHandler.HandleAsync(payload, cancellationToken);
        return matched
            ? Ok(new { status = "callback received" })
            : StatusCode(StatusCodes.Status304NotModified);
    }

    private static string BearerToken(string? authorization)
    {
        var value = authorization ?? string.Empty;
        var separator = value.IndexOf(' ');
        return separator < 0 ? value : value[(separator + 1)..];
    }
}
