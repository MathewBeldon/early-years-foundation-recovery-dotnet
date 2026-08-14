using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

public sealed class DashboardExportScheduler(IServiceScopeFactory scopeFactory, IHostEnvironment environment) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            await EnqueueIfDueAsync(stoppingToken);
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task EnqueueIfDueAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var today = DateTime.UtcNow.Date;
        var exists = await db.BackgroundJobs.AnyAsync(
            x => x.JobType == DashboardJob.JobType && x.CreatedAt >= today,
            cancellationToken);
        if (!exists)
        {
            await scope.ServiceProvider.GetRequiredService<IBackgroundJobService>()
                .EnqueueAsync(DashboardJob.JobType, new { scheduledFor = today }, cancellationToken: cancellationToken);
        }
    }
}
