using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// The smallest authenticated questionnaire submission contract: GET summative-q1,
/// submit answer 1 for a user with an existing passed module-2 assessment, then
/// follow each application's own redirect chain to summative-q2.
///
/// Rails v1.5.0 (ac546721) posts to responses#update with a submission nonce and
/// first redirects through the summative-q2 content page. .NET keeps the existing
/// questionnaire route and redirects directly to the final questionnaire page;
/// that intermediate redirect is an explicit accepted normalization.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string QuestionnaireEmail = "questionnaire@example.test";
    private const string QuestionnaireSub = "synthetic-questionnaire";
    private const string QuestionnaireName = "Questionnaire Learner";
    private const string QuestionnairePath = "/modules/module-2/questionnaires/summative-q1";
    private const string NextQuestionPath = "/modules/module-2/questionnaires/summative-q2";

    [ParityFact]
    public async Task Rails_and_dotnet_reuse_the_passed_assessment_and_reach_the_same_next_question()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, QuestionnaireEmail, QuestionnaireSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsGet = await CaptureQuestionAsync(rails, "Rails");
        var dotnetGet = await CaptureQuestionAsync(dotnet, ".NET");
        var railsPost = await SubmitFirstAnswerAsync(rails, railsGet, "Rails");
        var dotnetPost = await SubmitFirstAnswerAsync(dotnet, dotnetGet, ".NET");
        var differences = CompareQuestionnaire(railsGet, dotnetGet, railsPost, dotnetPost);

        await WriteQuestionnaireReportAsync(railsGet, dotnetGet, railsPost, dotnetPost, differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<QuestionnaireGetCapture> CaptureQuestionAsync(
        IAPIRequestContext context,
        string app)
    {
        var response = await FetchAsync(context, QuestionnairePath, app);
        var body = await response.TextAsync();
        var path = SanitizePath(QuestionnairePath);
        var form = FindQuestionnaireForm(body);

        return new(
            app,
            response.Status,
            path,
            Extract(HeadingRegex(), body),
            form.Action,
            form.TokenName,
            form.Token,
            form.Nonce,
            body.Contains("response[answers]", StringComparison.Ordinal),
            body.Contains(QuestionnaireSub, StringComparison.OrdinalIgnoreCase),
            body);
    }

    private static async Task<QuestionnaireSubmissionCapture> SubmitFirstAnswerAsync(
        IAPIRequestContext context,
        QuestionnaireGetCapture get,
        string app)
    {
        Ensure(
            get.Status == 200 && get.Path == QuestionnairePath,
            $"{app} status={get.Status} path={get.Path}. Expected authenticated GET {QuestionnairePath}. " +
            "Check the identity fixture, independent request context, and session cookie.");
        Ensure(!string.IsNullOrWhiteSpace(get.FormAction), $"{app} questionnaire form has no action. Check the Rails/.NET question view.");
        Ensure(!string.IsNullOrWhiteSpace(get.TokenName) && !string.IsNullOrWhiteSpace(get.Token), $"{app} questionnaire form has no CSRF token.");
        Ensure(!string.IsNullOrWhiteSpace(get.Nonce), $"{app} questionnaire form has no response[submission_nonce]. Check session persistence.");

        var form = context.CreateFormData();
        form.Set(get.TokenName!, get.Token!);
        form.Set("_method", "patch");
        form.Set("response[submission_nonce]", get.Nonce!);
        form.Set("response[answers]", "1");

        var response = await context.FetchAsync(get.FormAction!, new APIRequestContextOptions
        {
            Method = "POST",
            MaxRedirects = 0,
            FailOnStatusCode = false,
            Form = form,
        });
        var location = LocationOf(response);
        var chain = new List<string>();
        var final = response;
        var finalPath = SanitizePath(location);
        var finalBody = string.Empty;

        for (var hop = 0; hop < 4 && final.Status is >= 300 and < 400; hop++)
        {
            Ensure(!string.IsNullOrWhiteSpace(location), $"{app} submission status={final.Status} has no Location header.");
            finalPath = SanitizePath(location);
            chain.Add(finalPath);
            final = await FetchAsync(context, location!, app);
            finalBody = await final.TextAsync();
            location = LocationOf(final);
        }

        if (final.Status == 200 && finalBody.Length == 0)
        {
            finalBody = await final.TextAsync();
        }

        if (final.Status == 200)
        {
            finalPath = chain.Count == 0 ? QuestionnairePath : finalPath;
        }

        return new(
            app,
            response.Status,
            SanitizePath(LocationOf(response)),
            final.Status,
            finalPath,
            chain,
            Extract(HeadingRegex(), finalBody),
            finalBody.Contains("response[answers]", StringComparison.Ordinal),
            finalBody.Contains(QuestionnaireSub, StringComparison.OrdinalIgnoreCase),
            finalBody);
    }

    private static List<string> CompareQuestionnaire(
        QuestionnaireGetCapture railsGet,
        QuestionnaireGetCapture dotnetGet,
        QuestionnaireSubmissionCapture railsPost,
        QuestionnaireSubmissionCapture dotnetPost)
    {
        var differences = new List<string>();
        AddGetGates(differences, railsGet);
        AddGetGates(differences, dotnetGet);
        AddPostGates(differences, railsPost);
        AddPostGates(differences, dotnetPost);

        if (railsGet.Heading != dotnetGet.Heading)
            differences.Add(QuestionnaireEvidence(railsGet, dotnetGet, $"GET heading '{railsGet.Heading}' != '{dotnetGet.Heading}'"));
        if (railsPost.FinalStatus != dotnetPost.FinalStatus)
            differences.Add(QuestionnaireEvidence(railsPost, dotnetPost, $"final status {railsPost.FinalStatus} != {dotnetPost.FinalStatus}"));
        if (railsPost.FinalPath != dotnetPost.FinalPath)
            differences.Add(QuestionnaireEvidence(railsPost, dotnetPost, $"final path '{railsPost.FinalPath}' != '{dotnetPost.FinalPath}'"));
        if (railsPost.FinalHeading != dotnetPost.FinalHeading)
            differences.Add(QuestionnaireEvidence(railsPost, dotnetPost, $"final heading '{railsPost.FinalHeading}' != '{dotnetPost.FinalHeading}'"));
        if (railsPost.HasAnswerForm != dotnetPost.HasAnswerForm)
            differences.Add(QuestionnaireEvidence(railsPost, dotnetPost, "final answer form presence differs"));
        return differences.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void AddGetGates(List<string> differences, QuestionnaireGetCapture capture)
    {
        if (capture.Status != 200 || capture.Path != QuestionnairePath)
            differences.Add($"{capture.App} questionnaire GET status={capture.Status} path={capture.Path}; expected 200 at {QuestionnairePath}. " +
                            "Check the authenticated fixture and session cookies.");
        if (!capture.HasAnswerForm)
            differences.Add($"{capture.App} questionnaire GET did not render response[answers].");
        if (string.IsNullOrWhiteSpace(capture.Nonce))
            differences.Add($"{capture.App} questionnaire GET did not emit response[submission_nonce].");
        if (capture.ContainsRejectedEmail)
            differences.Add($"{capture.App} questionnaire GET contained an unexpected fixture identity.");
    }

    private static void AddPostGates(List<string> differences, QuestionnaireSubmissionCapture capture)
    {
        if (capture.SubmissionStatus is not >= 300 or not < 400)
            differences.Add($"{capture.App} questionnaire submission status={capture.SubmissionStatus}; expected a redirect.");
        if (capture.FinalStatus != 200 || capture.FinalPath != NextQuestionPath)
            differences.Add($"{capture.App} questionnaire final status={capture.FinalStatus} path={capture.FinalPath}; expected 200 at {NextQuestionPath}.");
        if (!capture.HasAnswerForm)
            differences.Add($"{capture.App} questionnaire final page did not render the next answer form.");
        if (capture.ContainsRejectedEmail)
            differences.Add($"{capture.App} questionnaire final page contained an unexpected fixture identity.");
    }

    private static string QuestionnaireEvidence(
        QuestionnaireGetCapture rails,
        QuestionnaireGetCapture dotnet,
        string difference) =>
        $"Rails GET status={rails.Status} path={rails.Path}; .NET GET status={dotnet.Status} path={dotnet.Path}: {difference}. " +
        "Do not normalize the final user-visible questionnaire path or page; inspect the live forms. " +
        "Only Rails' intermediate content-page redirect is accepted.";

    private static string QuestionnaireEvidence(
        QuestionnaireSubmissionCapture rails,
        QuestionnaireSubmissionCapture dotnet,
        string difference) =>
        $"Rails submission status={rails.SubmissionStatus} redirect={rails.InitialRedirect} final={rails.FinalStatus} {rails.FinalPath}; " +
        $".NET submission status={dotnet.SubmissionStatus} redirect={dotnet.InitialRedirect} final={dotnet.FinalStatus} {dotnet.FinalPath}: {difference}. " +
        "Only the intermediate Rails content-page redirect is accepted; final path/page must match.";

    private static async Task WriteQuestionnaireReportAsync(
        QuestionnaireGetCapture railsGet,
        QuestionnaireGetCapture dotnetGet,
        QuestionnaireSubmissionCapture railsPost,
        QuestionnaireSubmissionCapture dotnetPost,
        List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-questionnaire-parity.json"),
            JsonSerializer.Serialize(
                new
                {
                    scenario = "authenticated-questionnaire-submission",
                    risk = "P1",
                    expectedIdentity = new { email = QuestionnaireEmail, sub = QuestionnaireSub, name = QuestionnaireName },
                    rails = new { get = railsGet, submission = railsPost },
                    dotnet = new { get = dotnetGet, submission = dotnetPost },
                    acceptedNormalizations = new[]
                    {
                        "antiforgery token names and values",
                        "session cookie names",
                        "generated IDs and timestamps",
                        "HTML whitespace and framework markup",
                        "Rails intermediate content-page redirect versus direct .NET questionnaire redirect",
                        "assessment/response/event database identity details are reconciled by parity/reconcile.ps1",
                    },
                    differences,
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static QuestionnaireFormCapture FindQuestionnaireForm(string body)
    {
        foreach (Match match in FormRegex().Matches(body))
        {
            var formBody = match.Value;
            if (!formBody.Contains("response[submission_nonce]", StringComparison.Ordinal)
                || !formBody.Contains("response[answers]", StringComparison.Ordinal))
            {
                continue;
            }

            var tokenName = FindInputName(formBody, "__RequestVerificationToken")
                ?? FindInputName(formBody, "authenticity_token");
            var token = tokenName is null ? null : FindInputValue(formBody, tokenName);
            return new(
                SanitizePath(WebUtility.HtmlDecode(match.Groups["action"].Value)),
                tokenName,
                token,
                FindInputValue(formBody, "response[submission_nonce]"));
        }

        return new(null, null, null, null);
    }

    private static string? FindInputName(string body, string expectedName) =>
        InputRegex().Matches(body)
            .Select(match => WebUtility.HtmlDecode(match.Groups["name"].Value))
            .FirstOrDefault(name => name.Equals(expectedName, StringComparison.Ordinal));

    private static string? FindInputValue(string body, string name)
    {
        foreach (Match match in InputRegex().Matches(body))
        {
            if (WebUtility.HtmlDecode(match.Groups["name"].Value).Equals(name, StringComparison.Ordinal))
                return WebUtility.HtmlDecode(match.Groups["value"].Value);
        }

        return null;
    }

    [GeneratedRegex("<form\\b(?=[^>]*\\baction=\"(?<action>[^\"]+)\")[^>]*>.*?</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex FormRegex();

    [GeneratedRegex("<input\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")(?=[^>]*\\bvalue=\"(?<value>[^\"]*)\")[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex InputRegex();

    private sealed record QuestionnaireGetCapture(
        string App,
        int Status,
        string Path,
        string? Heading,
        string? FormAction,
        string? TokenName,
        string? Token,
        string? Nonce,
        bool HasAnswerForm,
        bool ContainsRejectedEmail,
        string Body);

    private sealed record QuestionnaireFormCapture(
        string? Action,
        string? TokenName,
        string? Token,
        string? Nonce);

    private sealed record QuestionnaireSubmissionCapture(
        string App,
        int SubmissionStatus,
        string InitialRedirect,
        int FinalStatus,
        string FinalPath,
        List<string> RedirectChain,
        string? FinalHeading,
        bool HasAnswerForm,
        bool ContainsRejectedEmail,
        string FinalBody);
}
