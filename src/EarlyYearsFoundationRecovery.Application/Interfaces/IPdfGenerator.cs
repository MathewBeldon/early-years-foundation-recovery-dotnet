namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IPdfGenerator
{
    Task<byte[]> GenerateCertificateAsync(string moduleName, string recipientName, CancellationToken cancellationToken = default);
}
