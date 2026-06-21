namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default);
}
