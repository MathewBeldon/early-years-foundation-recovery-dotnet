namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface INotifyCallbackHandler
{
    Task HandleAsync(string payload, CancellationToken cancellationToken = default);
}
