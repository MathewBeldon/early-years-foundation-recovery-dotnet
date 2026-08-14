using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using EarlyYearsFoundationRecovery.Infrastructure.Services;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[ApiController]
[Route("notify")]
public class NotifyController(
    INotifyCallbackHandler notifyCallbackHandler,
    IOptions<NotifyOptions> options) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Update(CancellationToken cancellationToken)
    {
        var authorization = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.Contains(options.Value.CallbackToken, StringComparison.Ordinal))
        {
            return Unauthorized(new { status = "invalid secure header" });
        }
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var matched = await notifyCallbackHandler.HandleAsync(payload, cancellationToken);
        return matched
            ? Ok(new { status = "callback received" })
            : StatusCode(StatusCodes.Status304NotModified);
    }
}
