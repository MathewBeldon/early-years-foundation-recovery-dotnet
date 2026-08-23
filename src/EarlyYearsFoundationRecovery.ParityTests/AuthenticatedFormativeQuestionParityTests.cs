using System.Net;
using System.Text.Json;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>Rails v1.5.0 ac546721 authenticated formative-question contract.</summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string FormativeEmail = "formative-questionnaire@example.test";
    private const string FormativeSub = "synthetic-formative-questionnaire";
    private const string FormativePath = "/modules/module-1/questionnaires/check-understanding";

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_formative_question_journey()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await ConfigureSimulatorAsync(simulator, FormativeEmail, FormativeSub);

        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsResult = await ExerciseFormativeAsync(rails, "Rails");
        var dotnetResult = await ExerciseFormativeAsync(dotnet, ".NET");
        var differences = CompareFormative(railsResult, dotnetResult);

        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(Path.Combine(reportDirectory, "authenticated-formative-question-parity.json"),
            JsonSerializer.Serialize(new
            {
                scenario = "authenticated-formative-question",
                risk = "P1",
                expectedIdentity = new { email = FormativeEmail, sub = FormativeSub },
                rails = railsResult,
                dotnet = dotnetResult,
                acceptedNormalizations = new[] { "framework HTML, antiforgery values, cookies, generated IDs, and timestamps" },
                differences,
            }, new JsonSerializerOptions { WriteIndented = true }));
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<FormativeCapture> ExerciseFormativeAsync(IAPIRequestContext context, string app)
    {
        var initial = await FetchAsync(context, FormativePath, app);
        var initialBody = await initial.TextAsync();
        var initialForm = FindQuestionnaireForm(initialBody);
        Ensure(initial.Status == 200 && initialForm.Action is not null && initialForm.TokenName is not null && initialForm.Token is not null,
            $"{app} formative GET did not expose a complete form.");

        var emptyData = context.CreateFormData();
        emptyData.Set(initialForm.TokenName!, initialForm.Token!);
        emptyData.Set("_method", "patch");
        var empty = await context.FetchAsync(initialForm.Action!, new APIRequestContextOptions
        {
            Method = "POST", MaxRedirects = 0, FailOnStatusCode = false, Form = emptyData,
        });
        var emptyBody = await empty.TextAsync();
        var retryForm = FindQuestionnaireForm(emptyBody);
        Ensure(retryForm.Action is not null && retryForm.TokenName is not null && retryForm.Token is not null,
            $"{app} formative 422 did not expose a retry form.");

        var answerData = context.CreateFormData();
        answerData.Set(retryForm.TokenName!, retryForm.Token!);
        answerData.Set("_method", "patch");
        answerData.Set("response[answers]", "2");
        var submitted = await context.FetchAsync(retryForm.Action!, new APIRequestContextOptions
        {
            Method = "POST", MaxRedirects = 0, FailOnStatusCode = false, Form = answerData,
        });
        var location = SanitizePath(LocationOf(submitted));
        var result = await FetchAsync(context, location, app);
        var resultBody = await result.TextAsync();
        var revisit = await FetchAsync(context, FormativePath, app);
        var revisitBody = await revisit.TextAsync();

        return new(
            app,
            initial.Status,
            empty.Status,
            emptyBody.Contains("select an answer", StringComparison.OrdinalIgnoreCase),
            submitted.Status,
            location,
            result.Status,
            ResultSemantics(resultBody),
            revisit.Status,
            ResultSemantics(revisitBody));
    }

    private static FormativeSemantics ResultSemantics(string body) => new(
        body.Contains("formative-results", StringComparison.Ordinal)
            || body.Contains("govuk-notification-banner", StringComparison.Ordinal),
        body.Contains("Correct answer", StringComparison.Ordinal),
        body.Contains("Wrong — pick Correct answer.", StringComparison.Ordinal),
        body.Contains("value=\"2\"", StringComparison.Ordinal) && body.Contains("checked", StringComparison.Ordinal),
        body.Contains("This is the correct answer", StringComparison.Ordinal)
            || body.Contains("Correct answer", StringComparison.Ordinal),
        body.Contains("disabled", StringComparison.Ordinal),
        !body.Contains("type=\"submit\" id=\"next-action\"", StringComparison.Ordinal),
        body.Contains("/modules/module-1/content-pages/assessment-intro", StringComparison.Ordinal));

    private static List<string> CompareFormative(FormativeCapture rails, FormativeCapture dotnet)
    {
        var differences = new List<string>();
        foreach (var capture in new[] { rails, dotnet })
        {
            if (capture.InitialStatus != 200) differences.Add($"{capture.App} initial GET={capture.InitialStatus}.");
            if (capture.EmptyStatus != 422) differences.Add($"{capture.App} empty answer returned {capture.EmptyStatus}; expected 422.");
            if (capture.SubmitStatus is not >= 300 or >= 400 || capture.RedirectPath != FormativePath)
                differences.Add($"{capture.App} valid answer redirected with {capture.SubmitStatus} to '{capture.RedirectPath}', expected '{FormativePath}'.");
            if (capture.ResultStatus != 200 || capture.RevisitStatus != 200 || capture.Result != new FormativeSemantics(true, true, true, true, true, true, true, true) || capture.Revisit != capture.Result)
                differences.Add($"{capture.App} did not preserve the complete read-only incorrect formative result on revisit.");
        }
        if (rails.EmptyStatus != dotnet.EmptyStatus || rails.RedirectPath != dotnet.RedirectPath || rails.Result != dotnet.Result || rails.Revisit != dotnet.Revisit)
            differences.Add("Rails and .NET formative journey semantics differ.");
        return differences;
    }

    private sealed record FormativeCapture(string App, int InitialStatus, int EmptyStatus, bool HasValidation, int SubmitStatus, string RedirectPath, int ResultStatus, FormativeSemantics Result, int RevisitStatus, FormativeSemantics Revisit);
    private sealed record FormativeSemantics(bool IncorrectBanner, bool CorrectAnswerText, bool FailureExplanation, bool WrongAnswerSelected, bool CorrectAnswerIdentified, bool ControlsDisabled, bool SubmitAbsent, bool ContinuesToAssessmentIntro);
}
