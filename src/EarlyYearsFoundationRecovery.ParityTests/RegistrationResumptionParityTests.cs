using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

public sealed partial class AuthenticatedAccountParityTests
{
    private const string ResumingEmail = "resuming@example.test";
    private const string ResumingSub = "synthetic-resuming";
    private const string CheckYourAnswersPath = "/registration/check-your-answers/edit";
    private const string ReviewTrainingEmailsPath = "/registration/training-emails/edit?return_to=check-your-answers";

    /// <summary>
    /// Rails v1.5.0 commit ac546721: Registration::BaseController#next_incomplete_step_path
    /// stops mandatory resumption checks after training_emails. A nil
    /// research_participant therefore does not prevent Check Your Answers.
    /// </summary>
    [ParityFact]
    public async Task Rails_and_dotnet_resume_the_same_late_registration_step()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, ResumingEmail, ResumingSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsStep = await CaptureRegistrationStepAsync(rails, "Rails");
        var dotnetStep = await CaptureRegistrationStepAsync(dotnet, ".NET");

        AssertRegistrationStep(railsStep);
        AssertRegistrationStep(dotnetStep);
        Assert.Equal(railsStep.Status, dotnetStep.Status);
        Assert.Equal(railsStep.Heading, dotnetStep.Heading);
        Assert.Equal(railsStep.HasContinue, dotnetStep.HasContinue);

        var railsTraining = await CapturePreferenceGetAsync(rails, ReviewTrainingEmailsPath, "training", "Rails");
        var dotnetTraining = await CapturePreferenceGetAsync(dotnet, ReviewTrainingEmailsPath, "training", ".NET");
        AssertReviewPreferenceGet(railsTraining);
        AssertReviewPreferenceGet(dotnetTraining);
        Assert.Equal(railsTraining.SelectedValue, dotnetTraining.SelectedValue);

        var railsValidation = await SubmitMissingPreferenceAsync(rails, railsTraining, "Rails");
        var dotnetValidation = await SubmitMissingPreferenceAsync(dotnet, dotnetTraining, ".NET");
        AssertMissingPreferenceValidation(railsValidation);
        AssertMissingPreferenceValidation(dotnetValidation);
        Assert.Equal(railsValidation.Status, dotnetValidation.Status);
        Assert.Equal(railsValidation.Heading, dotnetValidation.Heading);
    }

    private static async Task<RegistrationStepSemantics> CaptureRegistrationStepAsync(
        IAPIRequestContext context,
        string app)
    {
        var response = await FetchAsync(context, CheckYourAnswersPath, app);
        var body = await response.TextAsync();
        return new(
            app,
            response.Status,
            LocationOf(response),
            Extract(HeadingRegex(), body) ?? string.Empty,
            body.Contains("<button", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertRegistrationStep(RegistrationStepSemantics step)
    {
        Ensure(step.Status == 200,
            $"Expected {step.App} authenticated {CheckYourAnswersPath} to render, status={step.Status}, location={step.Location ?? "<none>"}.");
        Ensure(
            step.Heading.Contains("check", StringComparison.OrdinalIgnoreCase)
            && step.Heading.Contains("answer", StringComparison.OrdinalIgnoreCase),
            $"Check-your-answers heading was not recognisable: '{step.Heading}'.");
        Ensure(step.HasContinue, "Check Your Answers did not expose a submit action.");
    }

    private static void AssertReviewPreferenceGet(PreferenceGetCapture capture)
    {
        Ensure(capture.Status == 200,
            $"{capture.App} review GET status={capture.Status}; expected 200 at {ReviewTrainingEmailsPath}.");
        Ensure(!string.IsNullOrWhiteSpace(capture.FormAction),
            $"{capture.App} review GET did not expose a training-email form action.");
        Ensure(!string.IsNullOrWhiteSpace(capture.TokenName) && !string.IsNullOrWhiteSpace(capture.Token),
            $"{capture.App} review GET did not expose a CSRF token.");
        Ensure(capture.SelectedValue == "false",
            $"{capture.App} review GET selected '{capture.SelectedValue ?? "<none>"}'; expected the synthetic resuming preference false.");
    }

    private static async Task<RegistrationValidationSemantics> SubmitMissingPreferenceAsync(
        IAPIRequestContext context,
        PreferenceGetCapture get,
        string app)
    {
        var form = context.CreateFormData();
        form.Set(get.TokenName!, get.Token!);

        // Rails exposes PATCH through a POST form override; ASP.NET posts directly.
        // Both canonical submit routes omit /edit, so form actions are followed rather
        // than comparing the framework-specific verb representation.
        var isRails = app.Equals("Rails", StringComparison.Ordinal);
        if (isRails)
        {
            form.Set("_method", "patch");
            // Keep the required Rails user envelope present while deliberately
            // omitting user[training_emails]. The extra key is not permitted or saved.
            form.Set("user[_parity_validation_probe]", "1");
        }

        var response = await context.FetchAsync(get.FormAction!, new APIRequestContextOptions
        {
            Method = isRails ? "POST" : get.Method.ToUpperInvariant(),
            MaxRedirects = 0,
            FailOnStatusCode = false,
            Form = form,
        });
        var body = await response.TextAsync();
        return new(app, response.Status, LocationOf(response), Extract(HeadingRegex(), body) ?? string.Empty);
    }

    private static void AssertMissingPreferenceValidation(RegistrationValidationSemantics validation)
    {
        Ensure(validation.Status == 422,
            $"{validation.App} missing-preference submission status={validation.Status}, " +
            $"location={validation.Location ?? "<none>"}; expected an in-place 422 validation response.");
        Ensure(validation.Heading.Contains("email", StringComparison.OrdinalIgnoreCase),
            $"{validation.App} missing-preference validation heading was not recognisable: '{validation.Heading}'.");
    }

    private sealed record RegistrationStepSemantics(
        string App,
        int Status,
        string? Location,
        string Heading,
        bool HasContinue);

    private sealed record RegistrationValidationSemantics(
        string App,
        int Status,
        string? Location,
        string Heading);
}
