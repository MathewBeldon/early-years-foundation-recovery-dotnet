using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed class ModuleProgressService(
    IUserModuleProgressRepository progressRepository,
    ITrainingContentProvider contentProvider)
{
    public async Task<UserModuleProgress> RecordPageViewAsync(
        long userId,
        string moduleName,
        string pageName,
        CancellationToken cancellationToken = default)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken)
            ?? throw new InvalidOperationException($"Module '{moduleName}' not found.");

        return await RecordPageViewAsync(userId, module, pageName, cancellationToken);
    }

    public async Task<UserModuleProgress> RecordPageViewAsync(
        long userId,
        TrainingModuleContent module,
        string pageName,
        CancellationToken cancellationToken = default)
    {
        var page = module.PageByName(pageName)
            ?? throw new InvalidOperationException($"Page '{pageName}' not found.");

        var progress = await progressRepository.GetOrCreateAsync(userId, module.Name, cancellationToken);
        var now = DateTime.UtcNow;

        if (progress.StartedAt is null)
        {
            progress.StartedAt = now;
        }

        progress.LastPage = page.Name;
        progress.VisitedPages = page.PageType == "certificate"
            ? MarkAllContentPagesVisited(module, progress.VisitedPages)
            : MarkPageVisited(progress.VisitedPages, page.Name);

        if (page.PageType == "certificate" && progress.CompletedAt is null && progress.StartedAt is not null)
        {
            progress.CompletedAt = now;
        }

        await progressRepository.SaveAsync(progress, cancellationToken);
        return progress;
    }

    public int CalculatePercentage(UserModuleProgress? progress, TrainingModuleContent module) =>
        ModuleProgressDisplay.CalculatePercentage(progress, module);

    public TrainingPageContent? ResolveResumePage(UserModuleProgress? progress, TrainingModuleContent module)
    {
        if (progress?.CompletedAt is not null)
        {
            return module.CertificatePage ?? module.FirstContentPage;
        }

        if (!string.IsNullOrWhiteSpace(progress?.LastPage))
        {
            return module.PageByName(progress.LastPage) ?? module.FirstContentPage;
        }

        return module.FirstPage;
    }

    private static Dictionary<string, bool> MarkPageVisited(
        Dictionary<string, bool> visitedPages,
        string pageName)
    {
        var updated = new Dictionary<string, bool>(visitedPages, StringComparer.Ordinal)
        {
            [pageName] = true,
        };
        return updated;
    }

    private static Dictionary<string, bool> MarkAllContentPagesVisited(
        TrainingModuleContent module,
        Dictionary<string, bool> visitedPages)
    {
        var updated = new Dictionary<string, bool>(visitedPages, StringComparer.Ordinal);
        foreach (var contentPage in module.ContentPages)
        {
            updated[contentPage.Name] = true;
        }

        return updated;
    }
}
