namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

internal sealed class UnavailableContentfulModuleIntegrityCheck : IContentfulModuleIntegrityCheck
{
    public Task<IReadOnlyList<ContentfulModuleIntegrityResult>> CheckAllAsync(
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(
            "Contentful must be configured before a content_check job can run.");
}
