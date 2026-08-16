using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

public sealed partial class AuthenticatedAccountParityTests
{
    private const string ResumingEmail = "resuming@example.test";
    private const string ResumingSub = "synthetic-resuming";
    private const string ResearchParticipantEditPath = "/registration/research-participant/edit";

    /// <summary>
    /// Rails v1.5.0 commit ac546721: Registration::BaseController#next_incomplete_step_path
    /// treats a nil research_participant as the final outstanding answer after training_emails.
    /// The applications intentionally retain Rails' sign-in landing at terms and must both
    /// render the persisted user's resumable research step when it is revisited directly.
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
        Assert.Equal(railsStep.RadioLabels, dotnetStep.RadioLabels);
        Assert.Equal(railsStep.HasContinue, dotnetStep.HasContinue);
    }

    private static async Task<RegistrationStepSemantics> CaptureRegistrationStepAsync(
        IAPIRequestContext context,
        string app)
    {
        var response = await FetchAsync(context, ResearchParticipantEditPath, app);
        var body = await response.TextAsync();
        return new(
            app,
            response.Status,
            LocationOf(response),
            Extract(HeadingRegex(), body) ?? string.Empty,
            RadioLabels(body),
            body.Contains("Continue", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertRegistrationStep(RegistrationStepSemantics step)
    {
        Ensure(step.Status == 200,
            $"Expected {step.App} authenticated {ResearchParticipantEditPath} to render, status={step.Status}, location={step.Location ?? "<none>"}.");
        Ensure(
            step.Heading.Contains("willing", StringComparison.OrdinalIgnoreCase) &&
            step.Heading.Contains("improve", StringComparison.OrdinalIgnoreCase),
            $"Research-participant heading was not recognisable: '{step.Heading}'.");
        Ensure(step.RadioLabels.SequenceEqual(["Yes", "No"]),
            $"Research-participant choices were '{string.Join("', '", step.RadioLabels)}', expected Yes/No.");
        Ensure(step.HasContinue, "Research-participant page did not expose a Continue action.");
    }

    private static string[] RadioLabels(string html) =>
        Regex.Matches(
                html,
                @"<label\b[^>]*class=[""'][^""']*govuk-radios__label[^""']*[""'][^>]*>(?<text>.*?)</label>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline)
            .Select(match => WebUtility.HtmlDecode(Regex.Replace(match.Groups["text"].Value, "<[^>]+>", " ")))
            .Select(text => Regex.Replace(text, @"\s+", " ").Trim())
            .Where(text => text.Length > 0)
            .ToArray();

    private sealed record RegistrationStepSemantics(
        string App,
        int Status,
        string? Location,
        string Heading,
        string[] RadioLabels,
        bool HasContinue);
}
