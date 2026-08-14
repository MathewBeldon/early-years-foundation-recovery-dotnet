using EarlyYearsFoundationRecovery.Infrastructure.Services;
using UglyToad.PdfPig;

namespace EarlyYearsFoundationRecovery.ParityTests;

[Trait("Category", "Parity")]
public sealed class CertificatePdfTests
{
    // NOTE: this check generates a PDF in-process and never calls the .NET app, so
    // its real precondition is an installed Chromium rather than a base URL. It is
    // gated with the rest of the suite for now; see README for the follow-up.
    [ParityFact]
    public async Task Chromium_certificate_is_a_pdf_with_extractable_recipient_and_module_text()
    {
        ParityEnvironment.RequireDotnet();

        await using var generator = new ChromiumPdfGenerator();
        var bytes = await generator.GenerateCertificateAsync("Communication and language", "Synthetic Learner");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));

        using var document = PdfDocument.Open(bytes);
        var text = string.Join(" ", document.GetPages().Select(x => x.Text));
        Assert.Contains("Synthetic Learner", text);
        Assert.Contains("Communication and language", text);
    }
}
