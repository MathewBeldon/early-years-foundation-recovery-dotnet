using System.Globalization;
using System.Text;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class LocalFileAnalyticsExportService(
    ApplicationDbContext dbContext,
    IFileStorageService fileStorageService,
    ILogger<LocalFileAnalyticsExportService> logger) : IAnalyticsExportService
{
    public async Task<IReadOnlyList<string>> ExportDashboardAsync(CancellationToken cancellationToken = default)
    {
        var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var userCount = await dbContext.Users.CountAsync(cancellationToken);
        var completedModules = await dbContext.UserModuleProgress
            .CountAsync(p => p.CompletedAt != null, cancellationToken);

        var csv = new StringBuilder()
            .AppendLine("metric,value")
            .AppendLine(CultureInfo.InvariantCulture, $"registered_users,{userCount}")
            .AppendLine(CultureInfo.InvariantCulture, $"completed_modules,{completedModules}")
            .ToString();

        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var path = await fileStorageService.SaveAsync($"exports/{dateFolder}/dashboard.csv", stream, cancellationToken);

        logger.LogInformation("Stub analytics export written to {Path}", path);
        return [path];
    }
}
