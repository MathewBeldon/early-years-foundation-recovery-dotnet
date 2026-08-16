using System.Net;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Rails v1.5.0 certificate contract at
/// <c>GET /modules/:module/content-pages/:page(.pdf)</c>.
///
/// The result page remains assessment-gated, but Rails allows an authenticated
/// registered user to request the certificate page directly. A completed
/// progress row renders the recipient and date; incomplete or failed progress
/// renders the placeholder certificate and still returns 200.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string CertificatePath = "/modules/module-2/content-pages/certificate";
    private const string CertificatePdfPath = CertificatePath + ".pdf";
    private const string CompleteCertificateEmail = "certificate-complete@example.test";
    private const string CompleteCertificateSub = "synthetic-certificate-complete";
    private const string CompleteCertificateName = "Certificate Complete";
    private const string IncompleteCertificateEmail = "certificate-incomplete@example.test";
    private const string IncompleteCertificateSub = "synthetic-certificate-incomplete";
    private const string IncompleteCertificateName = "Certificate Incomplete";
    private const string CertificateModuleTitle = "Module 2";

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_authenticated_certificate_html_and_pdf_semantics()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });

        var complete = await CaptureCertificateScenarioAsync(
            playwright,
            simulator,
            railsUrl,
            dotnetUrl,
            CompleteCertificateEmail,
            CompleteCertificateSub,
            CompleteCertificateName,
            completed: true);
        var incomplete = await CaptureCertificateScenarioAsync(
            playwright,
            simulator,
            railsUrl,
            dotnetUrl,
            IncompleteCertificateEmail,
            IncompleteCertificateSub,
            IncompleteCertificateName,
            completed: false);

        var differences = new List<string>();
        differences.AddRange(CertificateGates(complete));
        differences.AddRange(CertificateGates(incomplete));
        differences.AddRange(CompareCertificateScenario(complete));
        differences.AddRange(CompareCertificateScenario(incomplete));

        await WriteCertificateReportAsync(complete, incomplete, differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<CertificateScenarioCapture> CaptureCertificateScenarioAsync(
        IPlaywright playwright,
        IAPIRequestContext simulator,
        string railsUrl,
        string dotnetUrl,
        string email,
        string sub,
        string expectedName,
        bool completed)
    {
        await ConfigureSimulatorAsync(simulator, email, sub);
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsCapture = await CaptureCertificateAppAsync(rails, "Rails", expectedName, completed);
        var dotnetCapture = await CaptureCertificateAppAsync(dotnet, ".NET", expectedName, completed);
        return new(email, sub, expectedName, completed, railsCapture, dotnetCapture);
    }

    private static async Task<CertificateAppCapture> CaptureCertificateAppAsync(
        IAPIRequestContext context,
        string app,
        string expectedName,
        bool completed)
    {
        var htmlResponse = await FetchAsync(context, CertificatePath, app);
        var htmlBody = await htmlResponse.TextAsync();
        var htmlLocation = LocationOf(htmlResponse);
        htmlResponse.Headers.TryGetValue("content-type", out var htmlContentType);

        var pdfResponse = await FetchAsync(context, CertificatePdfPath, app);
        var pdfBytes = await pdfResponse.BodyAsync();
        pdfResponse.Headers.TryGetValue("content-type", out var pdfContentType);
        pdfResponse.Headers.TryGetValue("content-disposition", out var contentDisposition);

        return new(
            app,
            expectedName,
            completed,
            new CertificateHtmlCapture(
                htmlResponse.Status,
                htmlResponse.Status is >= 300 and < 400 ? SanitizePath(htmlLocation) : CertificatePath,
                Extract(HeadingRegex(), htmlBody),
                htmlBody.Contains("Congratulations! You have now completed this module.", StringComparison.Ordinal),
                htmlBody.Contains("You have not yet completed the module.", StringComparison.Ordinal),
                htmlBody.Contains(completed ? expectedName : "Your name will appear here", StringComparison.Ordinal),
                htmlBody.Contains("Date completed:", StringComparison.Ordinal),
                htmlBody.Contains($"/modules/module-2/content-pages/certificate.pdf", StringComparison.Ordinal),
                htmlBody.Contains("stub", StringComparison.OrdinalIgnoreCase),
                htmlContentType),
            new CertificatePdfCapture(
                pdfResponse.Status,
                NormalizeMediaType(pdfContentType),
                contentDisposition,
                pdfBytes.Length >= 5 && Encoding.ASCII.GetString(pdfBytes, 0, 5) == "%PDF-",
                ExtractPdfText(pdfBytes),
                PdfPageCount(pdfBytes)));
    }

    private static List<string> CertificateGates(CertificateScenarioCapture scenario)
    {
        var failures = new List<string>();
        foreach (var capture in new[] { scenario.Rails, scenario.Dotnet })
        {
            var html = capture.Html;
            var pdf = capture.Pdf;
            var evidence = $"{capture.App} certificate ({scenario.Email}) HTML status={html.Status} path={html.Path}; PDF status={pdf.Status}";

            if (html.Status != 200 || html.Path != CertificatePath)
                failures.Add($"{evidence}: expected authenticated HTML 200 at {CertificatePath}.");
            if (html.Heading != "Get your certificate")
                failures.Add($"{evidence}: heading '{html.Heading ?? "<none>"}' != 'Get your certificate'.");
            if (scenario.Completed && (!html.HasCompletionCopy || !html.HasExpectedName || !html.HasDate))
                failures.Add($"{evidence}: completed certificate did not render recipient/date semantics.");
            if (!scenario.Completed && (!html.HasPlaceholderCopy || !html.HasExpectedName || html.HasDate))
                failures.Add($"{evidence}: incomplete certificate did not render placeholder/no-date semantics.");
            if (!html.HasCanonicalPdfLink)
                failures.Add($"{evidence}: missing canonical PDF link {CertificatePdfPath}.");
            if (html.ContainsStubWording)
                failures.Add($"{evidence}: contains obsolete stub wording.");

            if (pdf.Status != 200 || pdf.ContentType != "application/pdf")
                failures.Add($"{evidence}: expected PDF 200/application/pdf; observed {pdf.Status}/{pdf.ContentType ?? "<none>"}.");
            if (!pdf.HasSignature || pdf.PageCount != 1)
                failures.Add($"{evidence}: expected a one-page PDF with %PDF- signature; observed signature={pdf.HasSignature}, pages={pdf.PageCount?.ToString() ?? "<unreadable>"}.");
            var expectedText = scenario.Completed ? scenario.ExpectedName : "Your name will appear here";
            if (!pdf.Text.Contains(expectedText, StringComparison.Ordinal)
                || !pdf.Text.Contains(CertificateModuleTitle, StringComparison.Ordinal)
                || (scenario.Completed && !pdf.Text.Contains("Date completed", StringComparison.Ordinal))
                || (!scenario.Completed && pdf.Text.Contains("Date completed", StringComparison.Ordinal)))
            {
                failures.Add($"{evidence}: extracted PDF text did not prove the expected {(scenario.Completed ? "recipient/date" : "placeholder/no-date")} semantics. Text='{pdf.Text}'.");
            }

            if (capture.App == "Rails" && pdf.ContentDisposition is not null)
            {
                failures.Add($"{evidence}: Rails emitted Content-Disposition '{pdf.ContentDisposition}'. The pinned source does not add an application filename/disposition; stop and review the live Grover response rather than normalizing it.");
            }
        }

        return failures;
    }

    private static List<string> CompareCertificateScenario(CertificateScenarioCapture scenario)
    {
        var rails = scenario.Rails;
        var dotnet = scenario.Dotnet;
        var differences = new List<string>();
        var evidence = $"Rails certificate ({scenario.Email}) HTML status={rails.Html.Status} path={rails.Html.Path}; PDF status={rails.Pdf.Status}; .NET HTML status={dotnet.Html.Status} path={dotnet.Html.Path}; PDF status={dotnet.Pdf.Status}";

        if (rails.Html.Status != dotnet.Html.Status) differences.Add($"{evidence}: HTML status differs.");
        if (rails.Html.Path != dotnet.Html.Path) differences.Add($"{evidence}: HTML path differs.");
        if (rails.Html.Heading != dotnet.Html.Heading) differences.Add($"{evidence}: HTML heading differs.");
        if (rails.Html.HasCompletionCopy != dotnet.Html.HasCompletionCopy) differences.Add($"{evidence}: completion copy differs.");
        if (rails.Html.HasPlaceholderCopy != dotnet.Html.HasPlaceholderCopy) differences.Add($"{evidence}: placeholder copy differs.");
        if (rails.Html.HasExpectedName != dotnet.Html.HasExpectedName) differences.Add($"{evidence}: recipient/placeholder identity differs.");
        if (rails.Html.HasDate != dotnet.Html.HasDate) differences.Add($"{evidence}: completion-date presence differs.");
        if (rails.Html.HasCanonicalPdfLink != dotnet.Html.HasCanonicalPdfLink) differences.Add($"{evidence}: canonical PDF link presence differs.");
        if (rails.Pdf.Status != dotnet.Pdf.Status) differences.Add($"{evidence}: PDF status differs.");
        if (rails.Pdf.ContentType != dotnet.Pdf.ContentType) differences.Add($"{evidence}: PDF content type '{rails.Pdf.ContentType}' != '{dotnet.Pdf.ContentType}'.");
        if (rails.Pdf.ContentDisposition != dotnet.Pdf.ContentDisposition) differences.Add($"{evidence}: Content-Disposition '{rails.Pdf.ContentDisposition ?? "<absent>"}' != '{dotnet.Pdf.ContentDisposition ?? "<absent>"}'.");
        if (rails.Pdf.PageCount != dotnet.Pdf.PageCount) differences.Add($"{evidence}: PDF page count differs.");
        if (rails.Pdf.HasSignature != dotnet.Pdf.HasSignature) differences.Add($"{evidence}: PDF signature presence differs.");
        return differences;
    }

    private static async Task WriteCertificateReportAsync(
        CertificateScenarioCapture complete,
        CertificateScenarioCapture incomplete,
        List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-certificate-parity.json"),
            JsonSerializer.Serialize(
                new
                {
                    scenario = "authenticated-certificate-html-pdf",
                    pinnedRails = "ac546721",
                    complete,
                    incomplete,
                    acceptedNormalizations = new[]
                    {
                        "framework HTML markup and generated IDs",
                        "PDF bytes, producer metadata, timestamps, object IDs, and compression",
                    },
                    differences,
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
            return string.Empty;

        using var document = PdfDocument.Open(bytes);
        return NormalizeWhitespace(string.Join(" ", document.GetPages().Select(page => page.Text))) ?? string.Empty;
    }

    private static int? PdfPageCount(byte[] bytes)
    {
        if (bytes.Length < 5 || Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
            return null;

        using var document = PdfDocument.Open(bytes);
        return document.GetPages().Count();
    }

    private sealed record CertificateScenarioCapture(
        string Email,
        string Sub,
        string ExpectedName,
        bool Completed,
        CertificateAppCapture Rails,
        CertificateAppCapture Dotnet);

    private sealed record CertificateAppCapture(
        string App,
        string ExpectedName,
        bool Completed,
        CertificateHtmlCapture Html,
        CertificatePdfCapture Pdf);

    private sealed record CertificateHtmlCapture(
        int Status,
        string Path,
        string? Heading,
        bool HasCompletionCopy,
        bool HasPlaceholderCopy,
        bool HasExpectedName,
        bool HasDate,
        bool HasCanonicalPdfLink,
        bool ContainsStubWording,
        string? ContentType);

    private sealed record CertificatePdfCapture(
        int Status,
        string? ContentType,
        string? ContentDisposition,
        bool HasSignature,
        string Text,
        int? PageCount);
}
