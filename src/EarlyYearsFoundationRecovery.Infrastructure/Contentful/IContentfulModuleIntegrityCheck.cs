namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

internal interface IContentfulModuleIntegrityCheck
{
    Task<IReadOnlyList<ContentfulModuleIntegrityResult>> CheckAllAsync(
        CancellationToken cancellationToken = default);
}

internal sealed record ContentfulModuleIntegrityResult(string Name, bool IsValid);
