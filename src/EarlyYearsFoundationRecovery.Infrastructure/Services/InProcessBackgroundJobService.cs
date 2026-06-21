using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class InProcessBackgroundJobService(
    IServiceScopeFactory scopeFactory,
    ILogger<InProcessBackgroundJobService> logger) : IBackgroundJobService
{
    public void Enqueue<TJob>(Func<TJob, CancellationToken, Task> work) where TJob : class
    {
        ArgumentNullException.ThrowIfNull(work);

        _ = Task.Run(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            try
            {
                var job = scope.ServiceProvider.GetRequiredService<TJob>();
                await work(job, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "In-process background job failed for {JobType}", typeof(TJob).Name);
            }
        });
    }
}
