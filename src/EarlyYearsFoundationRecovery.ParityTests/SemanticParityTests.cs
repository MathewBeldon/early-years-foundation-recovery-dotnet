using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

[Trait("Category", "Parity")]
public sealed partial class SemanticParityTests
{
    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_public_semantics()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();

        var scenarios = JsonSerializer.Deserialize<List<Scenario>>(
            await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "scenarios.json")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        using var playwright = await Playwright.CreateAsync();
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });
        var report = new List<ParityResult>();

        foreach (var scenario in scenarios)
        {
            var railsResult = await CaptureAsync(rails, scenario);
            var dotnetResult = await CaptureAsync(dotnet, scenario);
            var differences = Compare(railsResult, dotnetResult);
            report.Add(new(scenario.Name, scenario.Risk, railsResult, dotnetResult,
                ["antiforgery values", "session cookie names", "generated IDs", "timestamps", "HTML whitespace"], differences));
        }

        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(Path.Combine(reportDirectory, "parity-report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Assert.True(report.All(x => x.Differences.Count == 0),
            string.Join(Environment.NewLine, report.Where(x => x.Differences.Count > 0)
                .Select(x => $"{x.Risk} {x.Scenario}: {string.Join("; ", x.Differences)}")));
    }

    private static async Task<SemanticResult> CaptureAsync(IAPIRequestContext context, Scenario scenario)
    {
        var response = await context.FetchAsync(scenario.Path, new()
        {
            Method = scenario.Method,
            MaxRedirects = 0,
            FailOnStatusCode = false,
        });
        var body = await response.TextAsync();
        response.Headers.TryGetValue("location", out var location);
        response.Headers.TryGetValue("content-type", out var contentType);
        response.Headers.TryGetValue("content-disposition", out var disposition);
        return new(
            response.Status,
            NormalizeLocation(location),
            Extract(HeadingRegex(), body),
            ExtractAll(ValidationRegex(), body),
            ExtractAll(NavigationRegex(), body),
            NormalizeWhitespace(contentType),
            NormalizeWhitespace(disposition));
    }

    private static List<string> Compare(SemanticResult rails, SemanticResult dotnet)
    {
        var differences = new List<string>();
        if (rails.Status != dotnet.Status) differences.Add($"status {rails.Status} != {dotnet.Status}");
        if (rails.Redirect != dotnet.Redirect) differences.Add($"redirect '{rails.Redirect}' != '{dotnet.Redirect}'");
        if (rails.Heading != dotnet.Heading) differences.Add($"heading '{rails.Heading}' != '{dotnet.Heading}'");
        if (!rails.ValidationMessages.SequenceEqual(dotnet.ValidationMessages)) differences.Add("validation messages differ");
        if (!rails.Navigation.SequenceEqual(dotnet.Navigation)) differences.Add("navigation differs");
        if (NormalizeMediaType(rails.ContentType) != NormalizeMediaType(dotnet.ContentType)) differences.Add("content type differs");
        if (rails.ContentDisposition != dotnet.ContentDisposition) differences.Add("download filename differs");
        return differences;
    }

    private static string? Extract(Regex regex, string body) =>
        regex.Match(body) is { Success: true } match ? NormalizeWhitespace(WebUtility.HtmlDecode(StripTags().Replace(match.Groups[1].Value, " "))) : null;

    private static List<string> ExtractAll(Regex regex, string body) => regex.Matches(body)
        .Select(x => NormalizeWhitespace(WebUtility.HtmlDecode(StripTags().Replace(x.Groups[1].Value, " "))))
        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList()!;

    private static string? NormalizeLocation(string? value) => value is null ? null : Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.PathAndQuery : value;
    private static string? NormalizeWhitespace(string? value) => value is null ? null : Whitespace().Replace(value, " ").Trim();
    private static string? NormalizeMediaType(string? value) => value?.Split(';', 2)[0].Trim().ToLowerInvariant();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any()) return directory.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    [GeneratedRegex("<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex HeadingRegex();
    [GeneratedRegex("<(?:span|a)[^>]*class=\"[^\"]*(?:error-message|govuk-error-message)[^\"]*\"[^>]*>(.*?)</(?:span|a)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex ValidationRegex();
    [GeneratedRegex("<a[^>]*(?:class=\"[^\"]*(?:govuk-header|govuk-service-navigation)[^\"]*\"|data-module=\"govuk-header\")[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex NavigationRegex();
    [GeneratedRegex("<[^>]+>")] private static partial Regex StripTags();
    [GeneratedRegex("\\s+")] private static partial Regex Whitespace();

    private sealed record Scenario(string Name, string Method, string Path, string Risk);
    private sealed record SemanticResult(int Status, string? Redirect, string? Heading, List<string> ValidationMessages, List<string> Navigation, string? ContentType, string? ContentDisposition);
    private sealed record ParityResult(string Scenario, string Risk, SemanticResult Rails, SemanticResult Dotnet, List<string> AcceptedNormalizations, List<string> Differences);
}
