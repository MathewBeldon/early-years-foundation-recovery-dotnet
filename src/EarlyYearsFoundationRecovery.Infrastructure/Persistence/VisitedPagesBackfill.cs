using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public static class VisitedPagesBackfill
{
    public static async Task RunAsync(
        ApplicationDbContext dbContext,
        ITrainingContentProvider contentProvider,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var completedProgress = await dbContext.UserModuleProgress
            .Where(p => p.CompletedAt != null)
            .ToListAsync(cancellationToken);

        if (completedProgress.Count == 0)
        {
            return;
        }

        var updated = 0;

        foreach (var progress in completedProgress)
        {
            var module = await contentProvider.GetModuleByNameAsync(progress.ModuleName, cancellationToken);
            if (module is null)
            {
                continue;
            }

            var contentPageNames = module.ContentPages
                .Select(p => p.Name)
                .ToHashSet(StringComparer.Ordinal);

            if (contentPageNames.All(name => progress.VisitedPages.ContainsKey(name)))
            {
                continue;
            }

            var merged = new Dictionary<string, bool>(progress.VisitedPages, StringComparer.Ordinal);
            foreach (var pageName in contentPageNames)
            {
                merged[pageName] = true;
            }

            progress.VisitedPages = merged;
            updated++;
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Backfilled visited_pages for {Count} completed module progress records.", updated);
        }
    }
}
