using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

public sealed class DashboardJob(
    IAnalyticsExportService analyticsExportService,
    ILogger<DashboardJob> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Running dashboard export job");
        await analyticsExportService.ExportDashboardAsync(cancellationToken);
    }
}
