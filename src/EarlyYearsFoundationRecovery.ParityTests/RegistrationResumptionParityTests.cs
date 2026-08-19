using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

public sealed partial class AuthenticatedAccountParityTests
{
    private const string ResumingEmail = "resuming@example.test";
    private const string ResumingSub = "synthetic-resuming";
    private const string CheckYourAnswersPath = "/registration/check-your-answers/edit";

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

    private sealed record RegistrationStepSemantics(
        string App,
        int Status,
        string? Location,
        string Heading,
        bool HasContinue);
}
