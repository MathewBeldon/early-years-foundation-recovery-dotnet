using EarlyYearsFoundationRecovery.Infrastructure.Services;
using UglyToad.PdfPig;

namespace EarlyYearsFoundationRecovery.ParityTests;

public sealed class CertificatePdfTests
{
    [Fact]
    public async Task Chromium_certificate_is_a_pdf_with_extractable_recipient_and_module_text()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_BASE_URL"))) return;

        await using var generator = new ChromiumPdfGenerator();
        var bytes = await generator.GenerateCertificateAsync("Communication and language", "Synthetic Learner");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        using var document = PdfDocument.Open(bytes);
        var text = string.Join(" ", document.GetPages().Select(x => x.Text));
        Assert.Contains("Synthetic Learner", text);
        Assert.Contains("Communication and language", text);
    }
}
