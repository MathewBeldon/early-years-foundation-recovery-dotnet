using Contentful.Core.Search;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulTrainingContentProvider(
    ContentfulClientFactory clientFactory,
    IContentfulContentCache contentCache,
    ILogger<ContentfulTrainingContentProvider> logger) : ITrainingContentProvider
{
    public async Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default)
    {
        var modules = await GetAllModulesAsync(cancellationToken);
        return modules.Where(module => module.Live).ToList();
    }

    public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) =>
        contentCache.GetOrCreateAsync(
            ContentfulContentCache.TrainingModulesKey,
            FetchModulesAsync,
            cancellationToken);

    public async Task<TrainingModuleContent?> GetModuleByNameAsync(
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        var modules = await GetAllModulesAsync(cancellationToken);
        return modules.FirstOrDefault(module =>
            string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TrainingPageContent?> GetPageAsync(
        string moduleName,
        string pageName,
        CancellationToken cancellationToken = default)
    {
        var module = await GetModuleByNameAsync(moduleName, cancellationToken);
        return module?.PageByName(pageName);
    }

    private async Task<IReadOnlyList<TrainingModuleContent>> FetchModulesAsync(CancellationToken cancellationToken)
    {
        using var activity = ContentfulTelemetry.ActivitySource.StartActivity(
            "Contentful fetch training modules",
            System.Diagnostics.ActivityKind.Client);
        activity?.SetTag("contentful.content_type", "trainingModule");
        activity?.SetTag("contentful.cache_key", ContentfulContentCache.TrainingModulesKey);

        try
        {
            var builder = QueryBuilder<TrainingModuleFields>.New
                .ContentTypeIs("trainingModule")
                .Include(10)
                .OrderBy("fields.position");

            var response = await clientFactory.Client.GetEntries(builder, cancellationToken);
            var modules = response
                .Select(ContentfulContentMapper.ToModule)
                .ToList();
            activity?.SetTag("contentful.result_count", modules.Count);

            return modules;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to load training modules from Contentful.");
            return [];
        }
    }
}
