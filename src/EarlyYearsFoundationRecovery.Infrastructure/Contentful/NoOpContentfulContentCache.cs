using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

internal sealed class NoOpContentfulContentCache : IContentfulContentCache
{
    public Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default) =>
        factory(cancellationToken);

    public void InvalidateAll()
    {
    }

    public void InvalidateForContentType(string contentTypeId)
    {
    }
}
