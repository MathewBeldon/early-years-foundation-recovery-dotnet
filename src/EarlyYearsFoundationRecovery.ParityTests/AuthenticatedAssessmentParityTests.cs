using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Authenticated GET assessment-result parity against live Rails v1.5.0 commit
/// ac546721 and .NET for the synthetic assessment learner
/// (assessment@example.test / synthetic-assessment).
///
/// One GET-only scenario: failed module-1 results, the retake target, and
/// passed module-2 results. Does not follow certificate/PDF, POST questionnaire
/// answers, or compare live/draft classification or event tracking.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string AssessmentEmail = "assessment@example.test";
    private const string AssessmentSub = "synthetic-assessment";
    private const string AssessmentName = "Assessment Learner";
    private const string FailedResultsPath = "/modules/module-1/assessment-result/assessment-results";
    private const string PassedResultsPath = "/modules/module-2/assessment-result/assessment-results";
    private const string RetakeTargetPath = "/modules/module-1/content-pages/assessment-intro";
    private const string CertificateContinuePath = "/modules/module-2/content-pages/certificate";
    private const int FailedScore = 50;
    private const int PassedScore = 75;

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_authenticated_assessment_result_semantics()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, AssessmentEmail, AssessmentSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsCapture = await CaptureAssessmentJourneyAsync(rails, "Rails");
        var dotnetCapture = await CaptureAssessmentJourneyAsync(dotnet, ".NET");

        var differences = AssessmentDifferences(railsCapture, dotnetCapture);
        await WriteAssessmentReportAsync(railsCapture, dotnetCapture, differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<AssessmentJourneyCapture> CaptureAssessmentJourneyAsync(
        IAPIRequestContext context,
        string app)
    {
        var failed = await CaptureAssessmentPageAsync(context, FailedResultsPath, app);
        var retake = await CaptureAssessmentPageAsync(context, RetakeTargetPath, app);
        var passed = await CaptureAssessmentPageAsync(context, PassedResultsPath, app);
        return new(app, failed, retake, passed);
    }

    private static async Task<AssessmentPageSemantics> CaptureAssessmentPageAsync(
        IAPIRequestContext context,
        string path,
        string app)
    {
        var response = await FetchAsync(context, path, app);
        var body = await response.TextAsync();
        response.Headers.TryGetValue("content-type", out var contentType);
        var location = LocationOf(response);
        var redirect = response.Status is >= 300 and < 400 ? SanitizePath(location) : null;
        var buttons = ActionButtons(body);
        var retake = buttons.FirstOrDefault(button =>
            button.Text.Equals("Retake test", StringComparison.OrdinalIgnoreCase));
        var myModules = buttons.FirstOrDefault(button =>
            SanitizePath(button.Href).Equals("/my-modules", StringComparison.Ordinal));
        var certificate = buttons.FirstOrDefault(button =>
            SanitizePath(button.Href).Equals(CertificateContinuePath, StringComparison.Ordinal));
        var continueAction = buttons.FirstOrDefault(button =>
            button.Text.Equals("Continue", StringComparison.OrdinalIgnoreCase));
        return new(
            response.Status,
            redirect ?? SanitizePath(path),
            redirect,
            Extract(HeadingRegex(), body),
            ExtractScore(body),
            OutcomeOf(body),
            myModules is not null,
            retake is not null,
            retake is null ? null : SanitizePath(retake.Href),
            certificate is null ? null : SanitizePath(certificate.Href),
            continueAction is not null,
            HasStartTest(body),
            ExtractAll(NavigationRegex(), body),
            contentType,
            body.Contains(RejectedEmail, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> AssessmentDifferences(AssessmentJourneyCapture rails, AssessmentJourneyCapture dotnet)
    {
        var differences = new List<string>();
        differences.AddRange(FailedGates(rails));
        differences.AddRange(FailedGates(dotnet));
        differences.AddRange(RetakeGates(rails));
        differences.AddRange(RetakeGates(dotnet));
        differences.AddRange(PassedGates(rails));
        differences.AddRange(PassedGates(dotnet));
        differences.AddRange(ComparePages("failed results", rails.Failed, dotnet.Failed));
        differences.AddRange(ComparePages("retake target", rails.Retake, dotnet.Retake));
        differences.AddRange(ComparePages("passed results", rails.Passed, dotnet.Passed));
        return differences.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> FailedGates(AssessmentJourneyCapture capture)
    {
        var result = capture.Failed;
        var evidence = AssessmentEvidence(capture.App, "failed results", result);
        var failures = new List<string>();
        if (result.Status != 200 || result.Redirect is not null || result.Path != FailedResultsPath)
            failures.Add($"{evidence}: expected 200, no redirect, final path {FailedResultsPath}. {AssessmentRemediation(capture.App, result.Status, result.Path)}");
        if (result.Score != FailedScore)
            failures.Add($"{evidence}: score {result.Score?.ToString() ?? "<none>"} != {FailedScore}. Seeded failed module-1 assessment must render.");
        if (result.Passed != false)
            failures.Add($"{evidence}: result was {DescribeOutcome(result.Passed)}, expected failed.");
        if (!result.HasMyModulesAction)
            failures.Add($"{evidence}: missing My modules action href /my-modules.");
        if (!result.HasRetakeAction || result.RetakeHref != RetakeTargetPath)
            failures.Add($"{evidence}: missing Retake test action to {RetakeTargetPath} (href={result.RetakeHref ?? "<none>"}).");
        if (result.CertificateHref is not null)
            failures.Add($"{evidence}: failed results must not expose a certificate continuation href ({result.CertificateHref}).");
        if (result.HasContinueAction)
            failures.Add($"{evidence}: failed results must not expose a Continue action.");
        if (result.ContainsRejectedEmail)
            failures.Add($"{evidence}: page contained {RejectedEmail}. Expected {AssessmentEmail} / {AssessmentSub}.");
        return failures;
    }

    private static List<string> RetakeGates(AssessmentJourneyCapture capture)
    {
        var result = capture.Retake;
        var evidence = AssessmentEvidence(capture.App, "retake target", result);
        var failures = new List<string>();
        if (result.Status != 200 || result.Redirect is not null || result.Path != RetakeTargetPath)
            failures.Add($"{evidence}: expected 200, no redirect, final path {RetakeTargetPath}. {AssessmentRemediation(capture.App, result.Status, result.Path)}");
        if (!result.HasStartTest)
            failures.Add($"{evidence}: missing Start test action. Do not follow the questionnaire href.");
        if (result.ContainsRejectedEmail)
            failures.Add($"{evidence}: page contained {RejectedEmail}. Expected {AssessmentEmail} / {AssessmentSub}.");
        return failures;
    }

    private static List<string> PassedGates(AssessmentJourneyCapture capture)
    {
        var result = capture.Passed;
        var evidence = AssessmentEvidence(capture.App, "passed results", result);
        var failures = new List<string>();
        if (result.Status != 200 || result.Redirect is not null || result.Path != PassedResultsPath)
            failures.Add($"{evidence}: expected 200, no redirect, final path {PassedResultsPath}. {AssessmentRemediation(capture.App, result.Status, result.Path)}");
        if (result.Score != PassedScore)
            failures.Add($"{evidence}: score {result.Score?.ToString() ?? "<none>"} != {PassedScore}. Seeded passed module-2 assessment must render.");
        if (result.Passed != true)
            failures.Add($"{evidence}: result was {DescribeOutcome(result.Passed)}, expected passed.");
        if (result.CertificateHref != CertificateContinuePath)
            failures.Add($"{evidence}: certificate continuation href '{result.CertificateHref ?? "<none>"}' != '{CertificateContinuePath}'. Do not follow certificate/PDF.");
        if (result.HasRetakeAction)
            failures.Add($"{evidence}: passed results must not expose a Retake test action.");
        if (result.ContainsRejectedEmail)
            failures.Add($"{evidence}: page contained {RejectedEmail}. Expected {AssessmentEmail} / {AssessmentSub}.");
        return failures;
    }

    private static List<string> ComparePages(string page, AssessmentPageSemantics rails, AssessmentPageSemantics dotnet)
    {
        var differences = new List<string>();
        if (rails.Status != dotnet.Status) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, $"status {rails.Status} != {dotnet.Status}"));
        if (rails.Redirect != dotnet.Redirect) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, $"redirect '{rails.Redirect}' != '{dotnet.Redirect}'"));
        if (rails.Score != dotnet.Score) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, $"score {rails.Score} != {dotnet.Score}"));
        if (rails.Passed != dotnet.Passed) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, $"result {DescribeOutcome(rails.Passed)} != {DescribeOutcome(dotnet.Passed)}"));
        if (rails.HasMyModulesAction != dotnet.HasMyModulesAction) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, "My modules action presence differs"));
        if (rails.HasRetakeAction != dotnet.HasRetakeAction) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, "Retake test action presence differs"));
        if (rails.RetakeHref != dotnet.RetakeHref) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, $"retake href '{rails.RetakeHref}' != '{dotnet.RetakeHref}'"));
        if (rails.CertificateHref != dotnet.CertificateHref) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, $"certificate href '{rails.CertificateHref}' != '{dotnet.CertificateHref}'"));
        if (rails.HasContinueAction != dotnet.HasContinueAction) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, "Continue action presence differs"));
        if (rails.HasStartTest != dotnet.HasStartTest) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, "Start test presence differs"));
        if (!rails.Navigation.SequenceEqual(dotnet.Navigation)) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, "signed-in navigation differs"));
        if (NormalizeMediaType(rails.ContentType) != NormalizeMediaType(dotnet.ContentType)) differences.Add(AssessmentCompareEvidence(page, rails, dotnet, "content type differs"));
        return differences;
    }

    private static string AssessmentCompareEvidence(
        string page,
        AssessmentPageSemantics rails,
        AssessmentPageSemantics dotnet,
        string difference) =>
        $"{page}: Rails status={rails.Status} path={rails.Path} redirect={rails.Redirect ?? "none"}; " +
        $".NET status={dotnet.Status} path={dotnet.Path} redirect={dotnet.Redirect ?? "none"}: {difference}. " +
        "Do not normalize this user-observable field or change assessment behavior, live/draft classification, or tracking; investigate the live pages.";

    private static string AssessmentEvidence(string app, string page, AssessmentPageSemantics result) =>
        $"{app} {page} status={result.Status} path={result.Path} redirect={result.Redirect ?? "none"}";

    private static string AssessmentRemediation(string app, int status, string path)
    {
        if (status is >= 300 and < 400)
            return "Authenticated assessment GETs must not redirect. Check the identity gate, fixture user, and that each app used its own IAPIRequestContext.";
        return Remediation(app, status, path);
    }

    private static async Task WriteAssessmentReportAsync(
        AssessmentJourneyCapture rails,
        AssessmentJourneyCapture dotnet,
        List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-assessment-parity.json"),
            System.Text.Json.JsonSerializer.Serialize(
                new AuthenticatedAssessmentParityReport(
                    "authenticated-assessment",
                    "P1",
                    new ExpectedIdentity(AssessmentEmail, AssessmentSub, AssessmentName),
                    rails.Failed,
                    rails.Retake,
                    rails.Passed,
                    dotnet.Failed,
                    dotnet.Retake,
                    dotnet.Passed,
                    [
                        "antiforgery values",
                        "session cookie names",
                        "generated IDs",
                        "timestamps",
                        "HTML whitespace",
                        "My modules button copy (href /my-modules is compared; Rails uses 'Go to my modules')",
                        "Start test questionnaire href (Rails content-pages redirect vs .NET /questionnaires)",
                        "incorrect-response review markup (no responses seeded)",
                        "certificate/PDF body (href only; not followed)",
                        "questionnaire answers (GET-only; no POST)",
                        "live/draft classification",
                    ],
                    differences),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<ActionButton> ActionButtons(string body) => GovukButtonRegex().Matches(body)
        .Select(match => new ActionButton(
            VisibleText(match.Groups["text"].Value),
            SanitizePath(WebUtility.HtmlDecode(match.Groups["href"].Value))))
        .Where(button => button.Href.Length > 0 && button.Href != "<none>")
        .ToList();

    private static int? ExtractScore(string body)
    {
        var match = ScoredPercentRegex().Match(body);
        return match.Success && int.TryParse(match.Groups[1].Value, out var score) ? score : null;
    }

    private static bool? OutcomeOf(string body)
    {
        var passed = body.Contains("You passed", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Congratulations", StringComparison.OrdinalIgnoreCase)
            || ScoredPassBannerRegex().IsMatch(body);
        var failed = body.Contains("You did not pass", StringComparison.OrdinalIgnoreCase)
            || body.Contains("below the pass mark", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Unfortunately", StringComparison.OrdinalIgnoreCase)
            || body.Contains("not scored highly enough", StringComparison.OrdinalIgnoreCase);
        if (passed == failed)
        {
            return null;
        }

        return passed;
    }

    private static bool HasStartTest(string body) =>
        GovukButtonRegex().Matches(body)
            .Select(match => VisibleText(match.Groups["text"].Value))
            .Any(text => text.Equals("Start test", StringComparison.OrdinalIgnoreCase))
        || NormalizeWhitespace(body)?.Contains("Start test", StringComparison.Ordinal) == true;

    private static string DescribeOutcome(bool? passed) => passed switch
    {
        true => "passed",
        false => "failed",
        null => "unknown",
    };

    [GeneratedRegex(
        "<a\\b(?=[^>]*\\bclass=\"[^\"]*govuk-button[^\"]*\")(?=[^>]*\\bhref=\"(?<href>[^\"]+)\")[^>]*>(?<text>.*?)</a>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GovukButtonRegex();

    [GeneratedRegex(@"You scored\s+(\d+)%", RegexOptions.IgnoreCase)]
    private static partial Regex ScoredPercentRegex();

    [GeneratedRegex(@"You scored\s+\d+%\s+[—-]\s+pass\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScoredPassBannerRegex();

    private sealed record AssessmentJourneyCapture(
        string App,
        AssessmentPageSemantics Failed,
        AssessmentPageSemantics Retake,
        AssessmentPageSemantics Passed);

    private sealed record AssessmentPageSemantics(
        int Status,
        string Path,
        string? Redirect,
        string? Heading,
        int? Score,
        bool? Passed,
        bool HasMyModulesAction,
        bool HasRetakeAction,
        string? RetakeHref,
        string? CertificateHref,
        bool HasContinueAction,
        bool HasStartTest,
        List<string> Navigation,
        string? ContentType,
        bool ContainsRejectedEmail);

    private sealed record ActionButton(string Text, string Href);

    private sealed record AuthenticatedAssessmentParityReport(
        string Scenario,
        string Risk,
        ExpectedIdentity ExpectedIdentity,
        AssessmentPageSemantics RailsFailed,
        AssessmentPageSemantics RailsRetake,
        AssessmentPageSemantics RailsPassed,
        AssessmentPageSemantics DotnetFailed,
        AssessmentPageSemantics DotnetRetake,
        AssessmentPageSemantics DotnetPassed,
        List<string> AcceptedNormalizations,
        List<string> Differences);
}
