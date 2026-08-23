using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>Rails v1.5.0 ac546721 authenticated video-page contract.</summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string VideoContentEmail = "video-content@example.test";
    private const string VideoContentSub = "synthetic-video-content";
    private const string VideoPath = "/modules/module-1/content-pages/expert-video";

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_authenticated_video_page()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, VideoContentEmail, VideoContentSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsResult = await ExerciseVideoAsync(rails, "Rails");
        var dotnetResult = await ExerciseVideoAsync(dotnet, ".NET");
        var differences = CompareVideo(railsResult, dotnetResult);
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(Path.Combine(reportDirectory, "authenticated-video-parity.json"),
            JsonSerializer.Serialize(new
            {
                scenario = "authenticated-video-content",
                rails = railsResult,
                dotnet = dotnetResult,
                acceptedNormalizations = new[]
                {
                    "Rails links to the question through /content-pages/check-understanding; .NET links directly to the canonical questionnaire route",
                    "the Rails iframe origin query parameter",
                    "framework HTML and CSS classes",
                },
                differences,
            },
                new JsonSerializerOptions { WriteIndented = true }));
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<VideoResult> ExerciseVideoAsync(IAPIRequestContext context, string app)
    {
        var first = await CaptureVideoAsync(context, app);
        var revisit = await CaptureVideoAsync(context, app);
        return new(app, first, revisit);
    }

    private static async Task<VideoPage> CaptureVideoAsync(IAPIRequestContext context, string app)
    {
        var response = await FetchAsync(context, VideoPath, app);
        var body = await response.TextAsync();
        var iframes = VideoIframeRegex().Matches(body);
        var iframe = iframes.Count == 0 ? Match.Empty : iframes[0];
        var source = WebUtility.HtmlDecode(iframe.Groups["src"].Value);
        var uri = Uri.TryCreate(source, UriKind.Absolute, out var parsed) ? parsed : null;
        var id = uri?.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        return new(
            response.Status,
            Extract(HeadingRegex(), body),
            NormalizeText(body).Contains("Watch an early years expert explain the approach.", StringComparison.Ordinal),
            iframes.Count,
            WebUtility.HtmlDecode(iframe.Groups["title"].Value),
            uri?.Scheme,
            uri?.Host == "www.youtube.com" ? "youtube" : uri?.Host,
            id,
            DetailsRegex().IsMatch(body),
            NormalizeText(body).Contains("Transcript", StringComparison.Ordinal),
            NormalizeText(body).Contains("The children have gone outside and started a bug hunt.", StringComparison.Ordinal),
            ActionHref(body, "previous-action"),
            ActionHref(body, "next-action"),
            body.Contains("[Video to be added]", StringComparison.Ordinal));
    }

    private static List<string> CompareVideo(VideoResult rails, VideoResult dotnet)
    {
        var differences = new List<string>();
        foreach (var result in new[] { rails, dotnet })
        {
            foreach (var (page, visit) in new[] { (result.First, "first"), (result.Revisit, "revisit") })
            {
                if (page.Status != 200) differences.Add($"{result.App} {visit} status was {page.Status}.");
                if (page.Heading != "Supporting children through play" || !page.HasBody) differences.Add($"{result.App} {visit} heading/body differed.");
                if (page.IframeCount != 1 || page.Scheme != "https" || page.Provider != "youtube" || page.VideoId != "XnP6jaK7ZAY") differences.Add($"{result.App} {visit} iframe semantics differed.");
                if (page.Title != "Supporting children through play") differences.Add($"{result.App} {visit} iframe title differed.");
                if (!page.HasDetails || !page.HasTranscriptSummary || !page.HasTranscriptText) differences.Add($"{result.App} {visit} transcript semantics differed.");
                if (page.Previous != "/modules/module-1/content-pages/applying-learning") differences.Add($"{result.App} {visit} previous href was '{page.Previous}'.");
                var expectedNext = result.App == "Rails"
                    ? "/modules/module-1/content-pages/check-understanding"
                    : "/modules/module-1/questionnaires/check-understanding";
                if (page.Next != expectedNext) differences.Add($"{result.App} {visit} next href was '{page.Next}'.");
                if (page.HasPlaceholder) differences.Add($"{result.App} {visit} rendered the video placeholder.");
            }
            if (result.First != result.Revisit) differences.Add($"{result.App} video semantics changed on revisit.");
        }
        if (NormalizeNext(rails.First) != NormalizeNext(dotnet.First)) differences.Add("Rails and .NET video semantics differ.");
        return differences;
    }

    private static VideoPage NormalizeNext(VideoPage page) => page with
    {
        Next = page.Next is "/modules/module-1/content-pages/check-understanding"
            or "/modules/module-1/questionnaires/check-understanding"
                ? "/modules/module-1/questionnaires/check-understanding"
                : page.Next,
    };

    private static string? ActionHref(string body, string id)
    {
        var pattern = "<a\\b(?=[^>]*\\bid=\"" + Regex.Escape(id) + "\")(?=[^>]*\\bhref=\"(?<href>[^\"]+)\")[^>]*>";
        var match = Regex.Match(body, pattern, RegexOptions.IgnoreCase);
        return match.Success ? SanitizePath(WebUtility.HtmlDecode(match.Groups["href"].Value)) : null;
    }

    private static string NormalizeText(string body) => Whitespace().Replace(WebUtility.HtmlDecode(StripTags().Replace(body, " ")), " ").Trim();

    [GeneratedRegex("<iframe\\b(?=[^>]*\\btitle=\"(?<title>[^\"]*)\")(?=[^>]*\\bsrc=\"(?<src>[^\"]+)\")[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex VideoIframeRegex();
    [GeneratedRegex("<details\\b[^>]*>.*?<summary\\b[^>]*>.*?Transcript.*?</summary>.*?</details>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex DetailsRegex();

    private sealed record VideoResult(string App, VideoPage First, VideoPage Revisit);
    private sealed record VideoPage(int Status, string? Heading, bool HasBody, int IframeCount, string Title, string? Scheme, string? Provider, string? VideoId, bool HasDetails, bool HasTranscriptSummary, bool HasTranscriptText, string? Previous, string? Next, bool HasPlaceholder);
}
