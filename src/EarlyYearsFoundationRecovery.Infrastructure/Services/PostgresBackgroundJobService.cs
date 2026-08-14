using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class PostgresBackgroundJobService(ApplicationDbContext dbContext) : IBackgroundJobService
{
    public async Task<long> EnqueueAsync(
        string jobType,
        object? payload = null,
        DateTime? runAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobType);
        var job = new BackgroundJob
        {
            JobType = jobType,
            Payload = JsonSerializer.Serialize(payload ?? new { }),
            RunAt = runAt ?? DateTime.UtcNow,
        };
        dbContext.BackgroundJobs.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
        return job.Id;
    }
}
