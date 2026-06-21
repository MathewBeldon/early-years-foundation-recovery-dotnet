using System.Text;
using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class StubPdfGenerator : IPdfGenerator
{
    public Task<byte[]> GenerateCertificateAsync(
        string moduleName,
        string recipientName,
        CancellationToken cancellationToken = default)
    {
        var content = $"""
            Certificate of completion

            Awarded to: {recipientName}
            Module: {moduleName}

            This is a stub certificate generated for local development.
            """;

        return Task.FromResult(Encoding.UTF8.GetBytes(content));
    }
}
