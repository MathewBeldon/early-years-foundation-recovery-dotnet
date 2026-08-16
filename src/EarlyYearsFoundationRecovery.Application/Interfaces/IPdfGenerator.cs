namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public sealed record CertificatePdfContent(
    string ModuleTitle,
    string RecipientName,
    DateTime? CompletedAt,
    string CriteriaHtml,
    bool IsCompleted);

public interface IPdfGenerator
{
    Task<byte[]> GenerateCertificateAsync(
        CertificatePdfContent certificate,
        CancellationToken cancellationToken = default);
}
