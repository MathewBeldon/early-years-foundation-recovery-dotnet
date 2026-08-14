namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IBackgroundJobService
{
    Task<long> EnqueueAsync(
        string jobType,
        object? payload = null,
        DateTime? runAt = null,
        CancellationToken cancellationToken = default);
}
