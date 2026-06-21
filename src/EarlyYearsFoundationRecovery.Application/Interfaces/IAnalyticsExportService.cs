namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IAnalyticsExportService
{
    Task<IReadOnlyList<string>> ExportDashboardAsync(CancellationToken cancellationToken = default);
}
