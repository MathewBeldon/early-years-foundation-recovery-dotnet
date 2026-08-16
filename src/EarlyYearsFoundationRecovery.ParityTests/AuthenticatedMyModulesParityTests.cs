using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Authenticated GET /my-modules parity against live Rails v1.5.0 commit ac546721
/// and .NET for the existing synthetic learner (existing@example.test / synthetic-existing).
///
/// Asserts only stable shared semantics: 200, My modules heading/intro/nav,
/// Rails-matching unstarted copy, and Rails-shaped module title hrefs
/// (<c>/modules/{name}</c> from <c>training_module_path(mod.name)</c>).
///
/// Available and upcoming bucket membership is not compared. Rails v1.5.0
/// <c>CourseProgress</c> splits those buckets with ContentIntegrity <c>draft?</c>;
/// .NET still uses the Contentful <c>live</c> flag. That live/draft difference
/// remains a known blocker and is out of scope for this slice.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string MyModulesPath = "/my-modules";
    private const string ExpectedHeading = "My modules";
    private const string ExpectedIntro =
        "You can complete the Early years child development training in any order. However, to support your understanding of the training, you may find it helpful to complete the modules in order.";
    private const string ExpectedUnstartedCopy =
        "You have not started any modules. To begin the training course, start an available module.";

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_authenticated_my_modules_semantics()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator);
        await SignInRailsAsync(rails);
        var railsCapture = await CaptureMyModulesAsync(rails, "Rails");
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");
        var dotnetCapture = await CaptureMyModulesAsync(dotnet, ".NET");

        var differences = MyModulesDifferences(railsCapture, dotnetCapture);
        await WriteMyModulesReportAsync(railsCapture.Semantics, dotnetCapture.Semantics, differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<MyModulesCapture> CaptureMyModulesAsync(IAPIRequestContext context, string app)
    {
        var response = await FetchAsync(context, MyModulesPath, app);
        var body = await response.TextAsync();
        response.Headers.TryGetValue("content-type", out var contentType);
        var location = LocationOf(response);
        var redirect = response.Status is >= 300 and < 400 ? SanitizePath(location) : null;
        var normalized = NormalizeWhitespace(body) ?? string.Empty;
        return new(
            app,
            new MyModulesSemanticResult(
                response.Status,
                redirect ?? MyModulesPath,
                redirect,
                Extract(HeadingRegex(), body),
                normalized.Contains(ExpectedIntro, StringComparison.Ordinal),
                normalized.Contains(ExpectedUnstartedCopy, StringComparison.Ordinal),
                ModuleTitleHrefs(body),
                ExtractAll(NavigationRegex(), body),
                contentType),
            body.Contains(RejectedEmail, StringComparison.OrdinalIgnoreCase));
    }

    private static List<string> MyModulesDifferences(MyModulesCapture rails, MyModulesCapture dotnet)
    {
        var differences = new List<string>();
        differences.AddRange(MyModulesGates(rails));
        differences.AddRange(MyModulesGates(dotnet));
        var a = rails.Semantics;
        var b = dotnet.Semantics;
        if (a.Status != b.Status) differences.Add(MyModulesCompareEvidence(a, b, $"status {a.Status} != {b.Status}"));
        if (a.Redirect != b.Redirect) differences.Add(MyModulesCompareEvidence(a, b, $"redirect '{a.Redirect}' != '{b.Redirect}'"));
        if (a.Heading != b.Heading) differences.Add(MyModulesCompareEvidence(a, b, $"heading '{a.Heading}' != '{b.Heading}'"));
        if (a.HasIntro != b.HasIntro) differences.Add(MyModulesCompareEvidence(a, b, "intro copy presence differs"));
        if (a.HasUnstartedCopy != b.HasUnstartedCopy) differences.Add(MyModulesCompareEvidence(a, b, "unstarted copy presence differs"));
        if (!a.Navigation.SequenceEqual(b.Navigation)) differences.Add(MyModulesCompareEvidence(a, b, "signed-in navigation differs"));
        if (NormalizeMediaType(a.ContentType) != NormalizeMediaType(b.ContentType)) differences.Add(MyModulesCompareEvidence(a, b, "content type differs"));
        return differences.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> MyModulesGates(MyModulesCapture capture)
    {
        var result = capture.Semantics;
        var evidence = $"{capture.App} status={result.Status} path={result.Path} redirect={result.Redirect ?? "none"}";
        var failures = new List<string>();
        if (result.Status != 200 || result.Redirect is not null || result.Path != MyModulesPath)
            failures.Add($"{evidence}: expected 200, no redirect, final path {MyModulesPath}. {MyModulesRemediation(capture.App, result.Status, result.Path)}");
        if (result.Heading != ExpectedHeading)
            failures.Add($"{evidence}: heading '{result.Heading}' != '{ExpectedHeading}'. {MyModulesRemediation(capture.App, result.Status, result.Path)}");
        if (!result.HasIntro)
            failures.Add($"{evidence}: missing My modules intro. {MyModulesRemediation(capture.App, result.Status, result.Path)}");
        if (!result.HasUnstartedCopy)
            failures.Add($"{evidence}: missing Rails unstarted copy for {ExpectedEmail}. {MyModulesRemediation(capture.App, result.Status, result.Path)}");
        if (result.ModuleTitleHrefs.Count == 0)
            failures.Add($"{evidence}: no Rails-shaped module title hrefs. Expected card-link--header links to /modules/{{name}}.");
        else if (result.ModuleTitleHrefs.Any(href => !IsRailsModuleTitleHref(href)))
            failures.Add($"{evidence}: module title hrefs were not Rails-shaped /modules/{{name}}: {string.Join(", ", result.ModuleTitleHrefs)}.");
        if (capture.ContainsRejectedEmail)
            failures.Add($"{evidence}: page contained {RejectedEmail}. Rejected. Expected existing synthetic learner {ExpectedEmail} / {ExpectedSub}.");
        return failures;
    }

    private static string MyModulesCompareEvidence(MyModulesSemanticResult rails, MyModulesSemanticResult dotnet, string difference) =>
        $"Rails status={rails.Status} path={rails.Path} redirect={rails.Redirect ?? "none"}; " +
        $".NET status={dotnet.Status} path={dotnet.Path} redirect={dotnet.Redirect ?? "none"}: {difference}. " +
        "Do not normalize this user-observable field or change app behavior; investigate the live pages. " +
        "Available/upcoming bucket membership is not compared (live/draft semantics).";

    private static async Task WriteMyModulesReportAsync(
        MyModulesSemanticResult rails,
        MyModulesSemanticResult dotnet,
        List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-my-modules-parity.json"),
            System.Text.Json.JsonSerializer.Serialize(
                new AuthenticatedMyModulesParityReport(
                    "authenticated-my-modules",
                    "P1",
                    new ExpectedIdentity(ExpectedEmail, ExpectedSub, ExpectedName),
                    rails,
                    dotnet,
                    [
                        "antiforgery values",
                        "session cookie names",
                        "generated IDs",
                        "timestamps",
                        "HTML whitespace",
                        "available/upcoming bucket membership (Rails draft? vs .NET live)",
                    ],
                    differences),
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    private static List<string> ModuleTitleHrefs(string body) => ModuleTitleHrefRegex().Matches(body)
        .Select(match => SanitizePath(WebUtility.HtmlDecode(match.Groups["href"].Value)))
        .Where(href => href.Length > 0 && href != "<none>")
        .Distinct(StringComparer.Ordinal)
        .ToList();

    private static bool IsRailsModuleTitleHref(string href) =>
        href.StartsWith("/modules/", StringComparison.Ordinal) &&
        href.Length > "/modules/".Length &&
        !href["/modules/".Length..].Contains('/');

    private static string MyModulesRemediation(string app, int status, string path)
    {
        if (status is >= 300 and < 400)
            return "Authenticated /my-modules must not redirect. Check the identity gate and that each app used its own IAPIRequestContext.";
        return Remediation(app, status, path);
    }

    [GeneratedRegex(
        "<a\\b(?=[^>]*\\bclass=\"[^\"]*card-link--header[^\"]*\")(?=[^>]*\\bhref=\"(?<href>[^\"]+)\")[^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ModuleTitleHrefRegex();

    private sealed record MyModulesCapture(string App, MyModulesSemanticResult Semantics, bool ContainsRejectedEmail);
    private sealed record MyModulesSemanticResult(
        int Status,
        string Path,
        string? Redirect,
        string? Heading,
        bool HasIntro,
        bool HasUnstartedCopy,
        List<string> ModuleTitleHrefs,
        List<string> Navigation,
        string? ContentType);
    private sealed record AuthenticatedMyModulesParityReport(
        string Scenario,
        string Risk,
        ExpectedIdentity ExpectedIdentity,
        MyModulesSemanticResult Rails,
        MyModulesSemanticResult Dotnet,
        List<string> AcceptedNormalizations,
        List<string> Differences);
}
