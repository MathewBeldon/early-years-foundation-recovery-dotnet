using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

public sealed class BackgroundJobWorker(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<BackgroundJobWorker> logger,
    IOptions<BackgroundJobOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private readonly BackgroundJobOptions _options = options.Value;
    internal string WorkerId => _workerId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        var recovery = RecoveryLoopAsync(stoppingToken);
        await PollLoopAsync(stoppingToken);
        await recovery;
    }

    private async Task PollLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessNextAsync(stoppingToken))
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job polling failed");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RecoveryLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.RecoveryInterval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverInterruptedJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job recovery scan failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    internal async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var candidates = await db.BackgroundJobs
            .FromSqlInterpolated($"""
                SELECT * FROM background_jobs
                WHERE status = 'queued' AND run_at <= {now}
                ORDER BY run_at, id
                FOR UPDATE SKIP LOCKED
                LIMIT 1
                """)
            .ToListAsync(cancellationToken);
        var job = candidates.SingleOrDefault();

        if (job is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        job.Status = "running";
        job.LockedAt = now;
        job.LockedBy = _workerId;
        job.Attempts++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        using var dispatchCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeat = HeartbeatAsync(job.Id, dispatchCancellation);
        Exception? failure = null;
        try
        {
            await DispatchAsync(scope.ServiceProvider, job, dispatchCancellation.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            await dispatchCancellation.CancelAsync();
        }

        var leaseHealthy = await heartbeat;
        if (!leaseHealthy)
        {
            logger.LogWarning("Background job {JobId} was left running for stale recovery because its lease could not be renewed", job.Id);
            return true;
        }

        if (failure is null)
        {
            var completed = await CompleteOwnedJobAsync(job.Id, cancellationToken);
            if (!completed)
            {
                logger.LogWarning("Background job {JobId} completed after this worker lost its lease; state was not overwritten", job.Id);
            }
        }
        else
        {
            var updated = await FailOwnedJobAsync(job, failure, cancellationToken);
            if (!updated)
            {
                logger.LogWarning(failure, "Background job {JobId} failed after this worker lost its lease; state was not overwritten", job.Id);
            }
        }

        return true;
    }

    internal async Task RecoverInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var staleBefore = now.Subtract(_options.LeaseTimeout);
        var failed = await db.BackgroundJobs
            .Where(x => x.Status == "running" && x.LockedAt < staleBefore && x.Attempts >= x.MaxAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.LastError, "Worker lease expired after the maximum number of attempts."), cancellationToken);
        var recovered = await db.BackgroundJobs
            .Where(x => x.Status == "running" && x.LockedAt < staleBefore && x.Attempts < x.MaxAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "queued")
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.RunAt, now), cancellationToken);
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {Count} interrupted background jobs", recovered);
        }
        if (failed > 0)
        {
            logger.LogError("Marked {Count} expired background jobs failed at their maximum attempt count", failed);
        }
    }

    private async Task<bool> HeartbeatAsync(
        long jobId,
        CancellationTokenSource dispatchCancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(_options.HeartbeatInterval, timeProvider);
            while (await timer.WaitForNextTickAsync(dispatchCancellation.Token))
            {
                if (!await RenewLeaseAsync(jobId, dispatchCancellation.Token))
                {
                    logger.LogWarning("Background job {JobId} lease ownership was lost", jobId);
                    await dispatchCancellation.CancelAsync();
                    return false;
                }
            }
            return true;
        }
        catch (OperationCanceledException) when (dispatchCancellation.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background job {JobId} heartbeat failed", jobId);
            await dispatchCancellation.CancelAsync();
            return false;
        }
    }

    internal async Task<bool> RenewLeaseAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var heartbeatAt = timeProvider.GetUtcNow().UtcDateTime;
        var affected = await db.BackgroundJobs
            .Where(x => x.Id == jobId && x.Status == "running" && x.LockedBy == _workerId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(x => x.LockedAt, heartbeatAt),
                cancellationToken);
        return affected == 1;
    }

    internal async Task<bool> CompleteOwnedJobAsync(long jobId, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var affected = await db.BackgroundJobs
            .Where(x => x.Id == jobId && x.Status == "running" && x.LockedBy == _workerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "completed")
                .SetProperty(x => x.CompletedAt, now)
                .SetProperty(x => x.LastError, (string?)null)
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null), cancellationToken);
        return affected == 1;
    }

    internal async Task<bool> FailOwnedJobAsync(
        BackgroundJob job,
        Exception failure,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var permanent = job.Attempts >= job.MaxAttempts;
        var owned = db.BackgroundJobs
            .Where(x => x.Id == job.Id && x.Status == "running" && x.LockedBy == _workerId);
        int affected;
        if (permanent)
        {
            affected = await owned.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.CompletedAt, (DateTime?)null)
                .SetProperty(x => x.LastError, failure.ToString())
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null), cancellationToken);
        }
        else
        {
            var retryAt = now.AddSeconds(Math.Pow(2, job.Attempts));
            affected = await owned.ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "queued")
                .SetProperty(x => x.RunAt, retryAt)
                .SetProperty(x => x.CompletedAt, (DateTime?)null)
                .SetProperty(x => x.LastError, failure.ToString())
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null), cancellationToken);
        }

        if (affected == 1)
        {
            if (permanent)
                logger.LogError(failure, "Background job {JobId} ({JobType}) failed permanently", job.Id, job.JobType);
            else
                logger.LogWarning(failure, "Background job {JobId} ({JobType}) will be retried", job.Id, job.JobType);
        }
        return affected == 1;
    }

    private static Task DispatchAsync(IServiceProvider services, BackgroundJob job, CancellationToken cancellationToken) =>
        job.JobType switch
        {
            DashboardJob.JobType => services.GetRequiredService<DashboardJob>().RunAsync(cancellationToken),
            ContentCheckJob.JobType => services.GetRequiredService<ContentCheckJob>().RunAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown background job type '{job.JobType}'."),
        };
}
