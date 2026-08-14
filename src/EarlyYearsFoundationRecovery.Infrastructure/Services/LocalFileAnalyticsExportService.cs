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
        var users = await dbContext.Users.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.CreatedAt, x.RegistrationComplete, x.RoleType, x.SettingType })
            .ToListAsync(cancellationToken);
        var events = await dbContext.Events.AsNoTracking().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.VisitId, x.UserId, x.Name, x.Time })
            .ToListAsync(cancellationToken);

        var csv = new StringBuilder()
            .AppendLine("metric,value")
            .AppendLine(CultureInfo.InvariantCulture, $"registered_users,{userCount}")
            .AppendLine(CultureInfo.InvariantCulture, $"completed_modules,{completedModules}")
            .ToString();

        var files = new Dictionary<string, string>
        {
            ["dashboard.csv"] = csv,
            ["users.csv"] = "id,created_at,registration_complete,role_type,setting_type\n" + string.Join("\n", users.Select(x =>
                FormattableString.Invariant($"{x.Id},{Iso(x.CreatedAt)},{x.RegistrationComplete},{Csv(x.RoleType)},{Csv(x.SettingType)}"))) + "\n",
            ["events.csv"] = "id,visit_id,user_id,name,time\n" + string.Join("\n", events.Select(x =>
                FormattableString.Invariant($"{x.Id},{x.VisitId},{x.UserId},{Csv(x.Name)},{Iso(x.Time)}"))) + "\n",
        };

        var paths = new List<string>();
        foreach (var file in files.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file.Value));
            paths.Add(await fileStorageService.SaveAsync($"exports/{dateFolder}/{file.Key}", stream, cancellationToken));
        }

        logger.LogInformation("Analytics export wrote {Count} deterministic CSV files", paths.Count);
        return paths;
    }

    private static string Iso(DateTime? value) => value?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Csv(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
