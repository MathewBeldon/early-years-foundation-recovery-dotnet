using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

public sealed class BackgroundJobWorker(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    ILogger<BackgroundJobWorker> logger) : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        await RecoverInterruptedJobsAsync(stoppingToken);

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

    internal async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var candidates = await db.BackgroundJobs
            .FromSqlInterpolated($"""
                SELECT * FROM background_jobs
                WHERE status = 'queued' AND run_at <= {DateTime.UtcNow}
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
        job.LockedAt = DateTime.UtcNow;
        job.LockedBy = _workerId;
        job.Attempts++;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        try
        {
            await DispatchAsync(scope.ServiceProvider, job, cancellationToken);
            job.Status = "completed";
            job.CompletedAt = DateTime.UtcNow;
            job.LastError = null;
        }
        catch (Exception ex)
        {
            job.LastError = ex.ToString();
            job.LockedAt = null;
            job.LockedBy = null;
            if (job.Attempts >= job.MaxAttempts)
            {
                job.Status = "failed";
                logger.LogError(ex, "Background job {JobId} ({JobType}) failed permanently", job.Id, job.JobType);
            }
            else
            {
                job.Status = "queued";
                job.RunAt = DateTime.UtcNow.AddSeconds(Math.Pow(2, job.Attempts));
                logger.LogWarning(ex, "Background job {JobId} ({JobType}) will be retried", job.Id, job.JobType);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task RecoverInterruptedJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleBefore = DateTime.UtcNow.AddMinutes(-15);
        var recovered = await db.BackgroundJobs
            .Where(x => x.Status == "running" && x.LockedAt < staleBefore)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, "queued")
                .SetProperty(x => x.LockedAt, (DateTime?)null)
                .SetProperty(x => x.LockedBy, (string?)null)
                .SetProperty(x => x.RunAt, DateTime.UtcNow), cancellationToken);
        if (recovered > 0)
        {
            logger.LogWarning("Recovered {Count} interrupted background jobs", recovered);
        }
    }

    private static Task DispatchAsync(IServiceProvider services, BackgroundJob job, CancellationToken cancellationToken) =>
        job.JobType switch
        {
            DashboardJob.JobType => services.GetRequiredService<DashboardJob>().RunAsync(cancellationToken),
            _ => throw new InvalidOperationException($"Unknown background job type '{job.JobType}'."),
        };
}
