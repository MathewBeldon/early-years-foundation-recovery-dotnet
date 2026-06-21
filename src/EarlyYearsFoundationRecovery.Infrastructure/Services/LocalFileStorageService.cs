using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class LocalFileStorageService(
    IOptions<InfrastructureOptions> options,
    ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    public async Task<string> SaveAsync(string relativePath, Stream content, CancellationToken cancellationToken = default)
    {
        var root = options.Value.StorageRootPath;
        Directory.CreateDirectory(root);

        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("Could not determine directory for storage path.");

        Directory.CreateDirectory(directory);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        logger.LogInformation("Saved file to {Path}", fullPath);
        return fullPath;
    }
}
