namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IContentfulContentCache
{
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);

    void InvalidateAll();

    void InvalidateForContentType(string contentTypeId);
}
