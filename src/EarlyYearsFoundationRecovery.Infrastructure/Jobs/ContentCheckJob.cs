using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

/// <summary>
/// Mirrors Rails v1.5.0 ContentCheckJob at
/// ac5467218a49c9de58a32a69d4edc01ce37710cf: reset the module cache, inspect
/// every ordered named module, warn for invalid content, and do not fail solely
/// because content is invalid.
/// </summary>
internal sealed class ContentCheckJob(
    IContentfulContentCache contentCache,
    IContentfulModuleIntegrityCheck integrityCheck,
    ILogger<ContentCheckJob> logger)
{
    public const string JobType = "content_check";

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        contentCache.InvalidateForContentType("trainingModule");
        var results = await integrityCheck.CheckAllAsync(cancellationToken);

        foreach (var result in results.Where(result => !result.IsValid))
        {
            logger.LogWarning(
                "Contentful module {ModuleName} failed the Rails content integrity contract",
                result.Name);
        }

        var invalidCount = results.Count(result => !result.IsValid);
        logger.LogInformation(
            "Contentful integrity check completed: {ModuleCount} modules checked, {InvalidModuleCount} invalid, {IsValid}",
            results.Count,
            invalidCount,
            invalidCount == 0);
    }
}
