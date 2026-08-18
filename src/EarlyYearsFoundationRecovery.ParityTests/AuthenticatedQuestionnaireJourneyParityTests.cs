using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

public sealed partial class AuthenticatedAccountParityTests
{
    private const string PassingJourneyEmail = "questionnaire-pass@example.test";
    private const string PassingJourneySub = "synthetic-questionnaire-pass";
    private const string FailingJourneyEmail = "questionnaire-fail@example.test";
    private const string FailingJourneySub = "synthetic-questionnaire-fail";

    [ParityFact]
    public async Task Rails_and_dotnet_complete_passing_and_failing_summative_journeys()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });

        var passing = await CaptureJourneyAsync(
            playwright,
            simulator,
            railsUrl,
            dotnetUrl,
            PassingJourneyEmail,
            PassingJourneySub,
            "module-1",
            ["2", "1"],
            expectedScore: 100,
            expectedPassed: true);
        var failing = await CaptureJourneyAsync(
            playwright,
            simulator,
            railsUrl,
            dotnetUrl,
            FailingJourneyEmail,
            FailingJourneySub,
            "module-1",
            ["1", "2"],
            expectedScore: 0,
            expectedPassed: false);

        var differences = passing.Differences.Concat(failing.Differences).Distinct(StringComparer.Ordinal).ToList();
        await File.WriteAllTextAsync(
            Path.Combine(FindRepositoryRoot(), "TestResults", "authenticated-questionnaire-journeys-parity.json"),
            JsonSerializer.Serialize(
                new
                {
                    scenario = "authenticated-questionnaire-journeys",
                    pinnedRails = "v1.5.0 / ac5467218a49c9de58a32a69d4edc01ce37710cf",
                    journeys = new[] { passing.Report, failing.Report },
                    acceptedNormalizations = new[]
                    {
                        "antiforgery values and session cookie names",
                        "generated IDs and timestamps",
                        "HTML whitespace and framework markup",
                        "Rails content-page intermediate redirects versus .NET questionnaire redirects",
                        "numeric JSON representation of completion-event scores",
                        "assessment, response, and event row IDs are reconciled separately by parity/reconcile.ps1",
                    },
                    differences,
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<JourneyCapture> CaptureJourneyAsync(
        IPlaywright playwright,
        IAPIRequestContext simulator,
        string railsUrl,
        string dotnetUrl,
        string email,
        string sub,
        string moduleName,
        IReadOnlyList<string> answers,
        int expectedScore,
        bool expectedPassed)
    {
        await ConfigureSimulatorAsync(simulator, email, sub);
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsJourney = await CaptureAppJourneyAsync(rails, "Rails", moduleName, answers, expectedScore, expectedPassed);
        var dotnetJourney = await CaptureAppJourneyAsync(dotnet, ".NET", moduleName, answers, expectedScore, expectedPassed);
        return new(email, sub, moduleName, railsJourney, dotnetJourney, CompareJourney(railsJourney, dotnetJourney));
    }

    private static async Task<AppJourneyCapture> CaptureAppJourneyAsync(
        IAPIRequestContext context,
        string app,
        string moduleName,
        IReadOnlyList<string> answers,
        int expectedScore,
        bool expectedPassed)
    {
        var submissions = new List<QuestionSubmissionCapture>();
        for (var index = 0; index < answers.Count; index++)
        {
            var questionName = $"summative-q{index + 1}";
            var path = $"/modules/{moduleName}/questionnaires/{questionName}";
            var get = await FetchAsync(context, path, app);
            var getBody = await get.TextAsync();
            var form = FindQuestionnaireForm(getBody);
            Ensure(get.Status == 200, $"{app} {path} GET status={get.Status}; expected 200.");
            Ensure(form.Action is not null && form.TokenName is not null && form.Token is not null && form.Nonce is not null,
                $"{app} {path} GET did not expose a complete authenticated answer form.");

            var data = context.CreateFormData();
            data.Set(form.TokenName!, form.Token!);
            data.Set("_method", "patch");
            data.Set("response[submission_nonce]", form.Nonce!);
            data.Set("response[answers]", answers[index]);
            var post = await context.FetchAsync(form.Action!, new APIRequestContextOptions
            {
                Method = "POST",
                MaxRedirects = 0,
                FailOnStatusCode = false,
                Form = data,
            });

            var chain = new List<string>();
            var response = post;
            var location = LocationOf(response);
            var currentPath = path;
            var finalBody = string.Empty;
            for (var hop = 0; hop < 6 && response.Status is >= 300 and < 400; hop++)
            {
                Ensure(!string.IsNullOrWhiteSpace(location), $"{app} {path} POST status={response.Status} had no Location.");
                chain.Add(SanitizePath(location));
                currentPath = SanitizePath(location);
                response = await FetchAsync(context, location!, app);
                finalBody = await response.TextAsync();
                location = LocationOf(response);
            }

            var finalPath = currentPath;
            if (response.Status == 200 && string.IsNullOrWhiteSpace(finalBody))
                finalBody = await response.TextAsync();

            submissions.Add(new(
                questionName,
                post.Status,
                chain,
                response.Status,
                finalPath,
                Extract(HeadingRegex(), finalBody),
                finalBody.Contains("response[answers]", StringComparison.Ordinal),
                finalBody));
        }

        var finalSubmission = submissions[^1];
        return new(
            app,
            moduleName,
            submissions,
            new AssessmentResultCapture(
                finalSubmission.FinalStatus,
                finalSubmission.FinalPath,
                ExtractScore(finalSubmission.FinalBody),
                OutcomeOf(finalSubmission.FinalBody),
                finalSubmission.FinalBody),
            expectedScore,
            expectedPassed);
    }

    private static List<string> CompareJourney(AppJourneyCapture rails, AppJourneyCapture dotnet)
    {
        var differences = new List<string>();
        AddJourneyGates(differences, rails);
        AddJourneyGates(differences, dotnet);
        if (rails.Submissions.Count != dotnet.Submissions.Count)
            differences.Add($"{rails.ModuleName}: submission count Rails={rails.Submissions.Count}, .NET={dotnet.Submissions.Count}.");

        for (var index = 0; index < Math.Min(rails.Submissions.Count, dotnet.Submissions.Count); index++)
        {
            var left = rails.Submissions[index];
            var right = dotnet.Submissions[index];
            if (left.SubmissionStatus != right.SubmissionStatus)
                differences.Add($"{rails.ModuleName} {left.QuestionName}: submission status Rails={left.SubmissionStatus}, .NET={right.SubmissionStatus}.");
            if (left.FinalStatus != right.FinalStatus)
                differences.Add($"{rails.ModuleName} {left.QuestionName}: final status Rails={left.FinalStatus}, .NET={right.FinalStatus}.");
            if (left.FinalPath != right.FinalPath && !EquivalentQuestionPath(left.FinalPath, right.FinalPath, rails.ModuleName, index + 2))
                differences.Add($"{rails.ModuleName} {left.QuestionName}: final path Rails='{left.FinalPath}', .NET='{right.FinalPath}'.");
            if (index < rails.Submissions.Count - 1 && left.HasAnswerForm != right.HasAnswerForm)
                differences.Add($"{rails.ModuleName} {left.QuestionName}: next answer form presence differs.");
        }

        if (rails.Result.Status != dotnet.Result.Status || rails.Result.Path != dotnet.Result.Path)
            differences.Add($"{rails.ModuleName}: result endpoint Rails={rails.Result.Status} {rails.Result.Path}, .NET={dotnet.Result.Status} {dotnet.Result.Path}.");
        if (rails.Result.Score != dotnet.Result.Score)
            differences.Add($"{rails.ModuleName}: result score Rails={rails.Result.Score}, .NET={dotnet.Result.Score}.");
        if (rails.Result.Passed != dotnet.Result.Passed)
            differences.Add($"{rails.ModuleName}: result status Rails={rails.Result.Passed}, .NET={dotnet.Result.Passed}.");
        return differences;
    }

    private static void AddJourneyGates(List<string> differences, AppJourneyCapture capture)
    {
        var result = capture.Result;
        if (result.Status != 200 || result.Path != $"/modules/{capture.ModuleName}/assessment-result/assessment-results")
            differences.Add($"{capture.App} {capture.ModuleName}: assessment results status={result.Status} path={result.Path}; expected authenticated 200 result page.");
        if (result.Score != capture.ExpectedScore)
            differences.Add($"{capture.App} {capture.ModuleName}: score={result.Score?.ToString() ?? "<none>"}; expected {capture.ExpectedScore}.");
        if (result.Passed != capture.ExpectedPassed)
            differences.Add($"{capture.App} {capture.ModuleName}: passed={result.Passed?.ToString() ?? "<unknown>"}; expected {capture.ExpectedPassed}.");
        foreach (var submission in capture.Submissions)
        {
            if (submission.SubmissionStatus is not >= 300 or not < 400)
                differences.Add($"{capture.App} {capture.ModuleName} {submission.QuestionName}: status={submission.SubmissionStatus}; expected redirect.");
            if (submission.FinalStatus != 200)
                differences.Add($"{capture.App} {capture.ModuleName} {submission.QuestionName}: final status={submission.FinalStatus}; expected 200.");
        }
    }

    private static bool EquivalentQuestionPath(string left, string right, string moduleName, int nextQuestionNumber) =>
        left == $"/modules/{moduleName}/content-pages/summative-q{nextQuestionNumber}"
        && right == $"/modules/{moduleName}/questionnaires/summative-q{nextQuestionNumber}";

    private sealed record JourneyCapture(
        string Email,
        string Sub,
        string ModuleName,
        AppJourneyCapture Rails,
        AppJourneyCapture Dotnet,
        List<string> Differences)
    {
        public object Report => new { email = Email, sub = Sub, moduleName = ModuleName, rails = Rails, dotnet = Dotnet, differences = Differences };
    }

    private sealed record AppJourneyCapture(
        string App,
        string ModuleName,
        List<QuestionSubmissionCapture> Submissions,
        AssessmentResultCapture Result,
        int ExpectedScore,
        bool ExpectedPassed);

    private sealed record QuestionSubmissionCapture(
        string QuestionName,
        int SubmissionStatus,
        List<string> RedirectChain,
        int FinalStatus,
        string FinalPath,
        string? FinalHeading,
        bool HasAnswerForm,
        string FinalBody);

    private sealed record AssessmentResultCapture(
        int Status,
        string Path,
        int? Score,
        bool? Passed,
        string Body);
}
