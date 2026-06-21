using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[ApiController]
[Route("notify")]
public class NotifyController(INotifyCallbackHandler notifyCallbackHandler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Update(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        await notifyCallbackHandler.HandleAsync(payload, cancellationToken);
        return Ok();
    }
}
