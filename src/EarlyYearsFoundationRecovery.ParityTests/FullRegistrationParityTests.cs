using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Rails v1.5.0 ac546721 first-registration contract. This drives the England
/// branch through a setting requiring a local authority, the custom-role link,
/// experience and both preferences before completing registration.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string FullRegistrationEmail = "full-registration@example.test";
    private const string FullRegistrationSub = "synthetic-full-registration";

    [ParityFact]
    public async Task Rails_and_dotnet_complete_the_same_England_registration_journey()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, FullRegistrationEmail, FullRegistrationSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsJourney = await CompleteRegistrationAsync(rails, "Rails");
        var dotnetJourney = await CompleteRegistrationAsync(dotnet, ".NET");

        Assert.Equal(railsJourney.Paths.Select(NormalizeRegistrationPath), dotnetJourney.Paths.Select(NormalizeRegistrationPath));
        Assert.Equal(railsJourney.ValidationHeading, dotnetJourney.ValidationHeading);
        Assert.Equal(railsJourney.FinalPath, dotnetJourney.FinalPath);
        Assert.Equal("/my-modules", railsJourney.FinalPath);
    }

    private static async Task<RegistrationJourneyCapture> CompleteRegistrationAsync(IAPIRequestContext context, string app)
    {
        var paths = new List<string>();
        var current = "/registration/terms-and-conditions/edit";

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["Accepted"] = "true",
            ["user[terms_and_conditions_agreed_at]"] = "2026-08-23T12:00:00Z",
        });
        paths.Add(current);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["FirstName"] = "Parity",
            ["LastName"] = "Registrant",
            ["user[first_name]"] = "Parity",
            ["user[last_name]"] = "Registrant",
        });
        paths.Add(current);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["CountryId"] = "england",
            ["user[where_you_live]"] = "England",
        });
        paths.Add(current);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["SettingTypeId"] = "setting_la_general_role",
            ["user[setting_type_id]"] = "setting_la_general_role",
        });
        paths.Add(current);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["LocalAuthorityId"] = "local_authority_a",
            ["user[local_authority]"] = "local_authority_a",
        });
        paths.Add(current);

        // The custom-role link is a conditional branch from the role page.
        var rolePage = await FetchAsync(context, current, app);
        Ensure(rolePage.Status == 200, $"{app} role GET status={rolePage.Status}.");
        var roleBody = await rolePage.TextAsync();
        Ensure(roleBody.Contains("/registration/role-type-other", StringComparison.Ordinal),
            $"{app} role page did not expose the custom-role branch.");
        current = app == "Rails" ? "/registration/role-type-other/edit" : "/registration/role-type-other";
        current = await SubmitStepAsync(context, app, current, new()
        {
            ["RoleTypeOther"] = "Early years parity specialist",
            ["user[role_type_other]"] = "Early years parity specialist",
        });
        paths.Add("/registration/role-type-other");
        paths.Add(current);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["ExperienceId"] = "2-5",
            ["user[early_years_experience]"] = "2-5",
        });
        paths.Add(current);

        var invalid = await SubmitStepAsync(context, app, current, new()
        {
            ["user[_parity_validation_probe]"] = "1",
        }, expectValidation: true);
        Ensure(invalid.Status == 422, $"{app} blank training-email status={invalid.Status}; expected 422.");
        var validationHeading = Clean(Extract(HeadingRegex(), invalid.Body) ?? string.Empty);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["TrainingEmails"] = "false",
            ["user[training_emails]"] = "false",
        });
        paths.Add(current);

        current = await SubmitStepAsync(context, app, current, new()
        {
            ["ResearchParticipant"] = "true",
            ["user[research_participant]"] = "true",
        });
        paths.Add(current);

        var summary = await FetchAsync(context, current, app);
        var summaryBody = await summary.TextAsync();
        Ensure(summary.Status == 200 && summaryBody.Contains("Parity Registrant", StringComparison.Ordinal),
            $"{app} check-your-answers did not render the completed details.");
        Ensure(summaryBody.Contains("Early years parity specialist", StringComparison.Ordinal),
            $"{app} check-your-answers did not render the custom role.");

        var completed = await SubmitStepAsync(context, app, current, new());
        return new(paths, validationHeading, completed);
    }

    private static async Task<string> SubmitStepAsync(
        IAPIRequestContext context,
        string app,
        string path,
        Dictionary<string, string> values)
    {
        var result = await SubmitStepAsync(context, app, path, values, expectValidation: false);
        Ensure(result.Status is >= 300 and < 400 && !string.IsNullOrWhiteSpace(result.Location),
            $"{app} submission at {path} status={result.Status}, location={result.Location ?? "<none>"}; expected redirect.");
        return SanitizePath(result.Location);
    }

    private static async Task<RegistrationSubmission> SubmitStepAsync(
        IAPIRequestContext context,
        string app,
        string path,
        Dictionary<string, string> values,
        bool expectValidation)
    {
        var get = await FetchAsync(context, path, app);
        var body = await get.TextAsync();
        Ensure(get.Status == 200, $"{app} GET {path} status={get.Status}; expected 200.");
        var action = NormalizeRegistrationPath(path);
        var formMatch = RegistrationFormRegex().Matches(body).Cast<Match>().FirstOrDefault(match =>
            NormalizeRegistrationPath(WebUtility.HtmlDecode(match.Groups["action"].Value)) == action);
        Ensure(formMatch is not null, $"{app} GET {path} did not expose the expected {action} form.");
        var formHtml = formMatch!.Value;
        var renderedAction = SanitizePath(WebUtility.HtmlDecode(formMatch.Groups["action"].Value));
        var token = RegistrationTokenRegex().Match(formHtml);
        Ensure(token.Success, $"{app} GET {path} did not expose a CSRF token.");

        var form = context.CreateFormData();
        form.Set(WebUtility.HtmlDecode(token.Groups["name"].Value), WebUtility.HtmlDecode(token.Groups["value"].Value));
        foreach (var (name, value) in values)
            form.Set(name, value);

        var isRails = app == "Rails";
        if (isRails)
            form.Set("_method", "patch");

        var response = await context.FetchAsync(action, new APIRequestContextOptions
        {
            Method = isRails ? "POST" : "POST",
            MaxRedirects = 0,
            FailOnStatusCode = false,
            Form = form,
        });
        var responseBody = expectValidation ? await response.TextAsync() : string.Empty;
        return new(response.Status, LocationOf(response), responseBody);
    }

    private static string Clean(string value) => Whitespace().Replace(StripTags().Replace(WebUtility.HtmlDecode(value), " "), " ").Trim();

    private static string NormalizeRegistrationPath(string path) =>
        Regex.Replace(SanitizePath(path), @"/edit$", string.Empty, RegexOptions.IgnoreCase);

    private sealed record RegistrationSubmission(int Status, string? Location, string Body);
    private sealed record RegistrationJourneyCapture(IReadOnlyList<string> Paths, string ValidationHeading, string FinalPath);

    [GeneratedRegex("<form\\b(?=[^>]*\\baction=\\\"(?<action>[^\\\"]+)\\\")[^>]*>.*?</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegistrationFormRegex();

    [GeneratedRegex("<input\\b(?=[^>]*\\bname=\\\"(?<name>__RequestVerificationToken|authenticity_token)\\\")(?=[^>]*\\bvalue=\\\"(?<value>[^\\\"]+)\\\")[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex RegistrationTokenRegex();
}
