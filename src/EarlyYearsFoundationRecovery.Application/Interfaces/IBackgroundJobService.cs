namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IBackgroundJobService
{
    void Enqueue<TJob>(Func<TJob, CancellationToken, Task> work) where TJob : class;
}
