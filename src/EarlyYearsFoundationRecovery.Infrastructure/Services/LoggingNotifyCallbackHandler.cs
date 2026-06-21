using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class LoggingNotifyCallbackHandler(ILogger<LoggingNotifyCallbackHandler> logger) : INotifyCallbackHandler
{
    public Task HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Stub Notify callback received: {Payload}", payload);
        return Task.CompletedTask;
    }
}
