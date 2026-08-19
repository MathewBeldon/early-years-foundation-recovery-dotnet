using Contentful.Core.Search;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

/// <summary>
/// Strict background-job boundary matching Rails v1.5.0 ContentCheckJob at
/// ac5467218a49c9de58a32a69d4edc01ce37710cf. Unlike the public-page provider,
/// operational Contentful failures are allowed to escape so the job is retried.
/// </summary>
internal sealed class ContentfulModuleIntegrityCheck(ContentfulClientFactory clientFactory)
    : IContentfulModuleIntegrityCheck
{
    public async Task<IReadOnlyList<ContentfulModuleIntegrityResult>> CheckAllAsync(
        CancellationToken cancellationToken = default)
    {
        var builder = QueryBuilder<TrainingModuleFields>.New
            .ContentTypeIs("trainingModule")
            .Include(10)
            .OrderBy("fields.position");

        var response = await clientFactory.Client.GetEntries(builder, cancellationToken);
        return response
            .Where(module => !string.IsNullOrWhiteSpace(module.Name))
            .Select(module => new ContentfulModuleIntegrityResult(
                module.Name,
                ContentfulModuleIntegrity.IsValid(module, module.Pages ?? [])))
            .ToList();
    }
}
