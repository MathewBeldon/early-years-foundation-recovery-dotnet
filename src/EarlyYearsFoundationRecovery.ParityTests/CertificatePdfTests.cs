using EarlyYearsFoundationRecovery.Infrastructure.Services;
using UglyToad.PdfPig;

namespace EarlyYearsFoundationRecovery.ParityTests;

[Trait("Category", "Parity")]
public sealed class CertificatePdfTests
{
    [ParityFact]
    public async Task Chromium_certificate_is_a_pdf_with_extractable_completed_content()
    {
        ParityEnvironment.RequireDotnet();

        await using var generator = new ChromiumPdfGenerator();
        var bytes = await generator.GenerateCertificateAsync(new(
            "Communication and language",
            "Synthetic Learner",
            new DateTime(2026, 1, 15),
            "<ul><li>Communication</li><li>Language</li></ul>",
            true));
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        using var document = PdfDocument.Open(bytes);
        var text = string.Join(" ", document.GetPages().Select(x => x.Text));
        Assert.Contains("Synthetic Learner", text);
        Assert.Contains("Communication and language", text);
        Assert.Contains("Date completed", text);
        Assert.Contains("Communication", text);
    }

    [ParityFact]
    public async Task Chromium_certificate_is_a_pdf_with_extractable_placeholder_content()
    {
        ParityEnvironment.RequireDotnet();

        await using var generator = new ChromiumPdfGenerator();
        var bytes = await generator.GenerateCertificateAsync(new(
            "Communication and language",
            "Your name will appear here",
            null,
            "<ul><li>Communication</li></ul>",
            false));

        using var document = PdfDocument.Open(bytes);
        var text = string.Join(" ", document.GetPages().Select(x => x.Text));
        Assert.Contains("Your name will appear here", text);
        Assert.DoesNotContain("Date completed", text);
    }
}
