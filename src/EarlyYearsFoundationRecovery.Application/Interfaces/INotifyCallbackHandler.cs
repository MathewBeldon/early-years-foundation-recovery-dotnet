namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface INotifyCallbackHandler
{
    Task<bool> HandleAsync(string payload, CancellationToken cancellationToken = default);
}
