using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Rails v1.5.0 ac546721 account preference contract:
/// registration/training_emails_controller.rb and
/// registration/research_participants_controller.rb track named events with
/// success true/false, render invalid forms as 422, and redirect a registered
/// user to /my-account after a successful PATCH update.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string AccountPreferencesEmail = "account-preferences@example.test";
    private const string AccountPreferencesSub = "synthetic-account-preferences";
    private const string AccountPreferencesName = "Account Preferences";
    private const string TrainingEmailsPath = "/registration/training-emails/edit";
    private const string ResearchParticipantPath = "/registration/research-participant/edit";

    [ParityFact]
    public async Task Rails_and_dotnet_preserve_account_preferences_across_authenticated_edits()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, AccountPreferencesEmail, AccountPreferencesSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsTrainingGet = await CapturePreferenceGetAsync(rails, TrainingEmailsPath, "training", "Rails");
        var dotnetTrainingGet = await CapturePreferenceGetAsync(dotnet, TrainingEmailsPath, "training", ".NET");
        var railsTrainingPost = await SubmitPreferenceAsync(rails, railsTrainingGet, "false", "Rails");
        var dotnetTrainingPost = await SubmitPreferenceAsync(dotnet, dotnetTrainingGet, "false", ".NET");

        var railsResearchGet = await CapturePreferenceGetAsync(rails, ResearchParticipantPath, "research", "Rails");
        var dotnetResearchGet = await CapturePreferenceGetAsync(dotnet, ResearchParticipantPath, "research", ".NET");
        var railsResearchPost = await SubmitPreferenceAsync(rails, railsResearchGet, "false", "Rails");
        var dotnetResearchPost = await SubmitPreferenceAsync(dotnet, dotnetResearchGet, "false", ".NET");

        var railsAccount = await CaptureMyAccountAsync(rails, "Rails");
        var dotnetAccount = await CaptureMyAccountAsync(dotnet, ".NET");
        var differences = ComparePreferenceScenario(
            railsTrainingGet,
            dotnetTrainingGet,
            railsTrainingPost,
            dotnetTrainingPost,
            railsResearchGet,
            dotnetResearchGet,
            railsResearchPost,
            dotnetResearchPost,
            railsAccount,
            dotnetAccount);

        await WritePreferenceReportAsync(
            railsTrainingGet,
            dotnetTrainingGet,
            railsTrainingPost,
            dotnetTrainingPost,
            railsResearchGet,
            dotnetResearchGet,
            railsResearchPost,
            dotnetResearchPost,
            railsAccount,
            dotnetAccount,
            differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<PreferenceGetCapture> CapturePreferenceGetAsync(
        IAPIRequestContext context,
        string path,
        string preference,
        string app)
    {
        var response = await FetchAsync(context, path, app);
        var body = await response.TextAsync();
        var form = FindPreferenceForm(body, preference);
        return new(
            app,
            preference,
            response.Status,
            SanitizePath(path),
            form.Action,
            form.Method,
            form.TokenName,
            form.Token,
            form.FieldName,
            form.SelectedValue,
            body);
    }

    private static async Task<PreferenceSubmissionCapture> SubmitPreferenceAsync(
        IAPIRequestContext context,
        PreferenceGetCapture get,
        string value,
        string app)
    {
        Ensure(
            get.Status == 200 && get.Path == (get.Preference == "training" ? TrainingEmailsPath : ResearchParticipantPath),
            $"{app} {get.Preference} GET status={get.Status} path={get.Path}; expected 200 at {get.Path}.");
        Ensure(
            !string.IsNullOrWhiteSpace(get.FormAction),
            $"{app} {get.Preference} form has no action. First form evidence: {FirstFormEvidence(get.Body)}. " +
            $"Preference evidence: {PreferenceEvidence(get.Body, get.Preference)}");
        Ensure(!string.IsNullOrWhiteSpace(get.TokenName) && !string.IsNullOrWhiteSpace(get.Token), $"{app} {get.Preference} form has no CSRF token.");
        Ensure(!string.IsNullOrWhiteSpace(get.FieldName), $"{app} {get.Preference} form has no preference field.");
        Ensure(!string.IsNullOrWhiteSpace(get.SelectedValue), $"{app} {get.Preference} form has no selected current value.");

        var form = context.CreateFormData();
        form.Set(get.TokenName!, get.Token!);
        form.Set(get.FieldName!, value);
        var isRails = app.Equals("Rails", StringComparison.Ordinal);
        if (isRails)
        {
            form.Set("_method", "patch");
        }

        var response = await context.FetchAsync(get.FormAction!, new APIRequestContextOptions
        {
            Method = isRails ? "POST" : get.Method.ToUpperInvariant(),
            MaxRedirects = 0,
            FailOnStatusCode = false,
            Form = form,
        });
        var location = LocationOf(response);
        var final = !string.IsNullOrWhiteSpace(location)
            ? await FetchAsync(context, location!, app)
            : response;
        var finalBody = final == response ? await response.TextAsync() : await final.TextAsync();
        var finalPath = final == response ? SanitizePath(get.Path) : SanitizePath(location);

        return new(
            app,
            get.Preference,
            response.Status,
            SanitizePath(location),
            final.Status,
            finalPath,
            Extract(HeadingRegex(), finalBody),
            finalBody);
    }

    private static List<string> ComparePreferenceScenario(
        PreferenceGetCapture railsTrainingGet,
        PreferenceGetCapture dotnetTrainingGet,
        PreferenceSubmissionCapture railsTrainingPost,
        PreferenceSubmissionCapture dotnetTrainingPost,
        PreferenceGetCapture railsResearchGet,
        PreferenceGetCapture dotnetResearchGet,
        PreferenceSubmissionCapture railsResearchPost,
        PreferenceSubmissionCapture dotnetResearchPost,
        Capture railsAccount,
        Capture dotnetAccount)
    {
        var differences = new List<string>();
        AddPreferenceGetGates(differences, railsTrainingGet);
        AddPreferenceGetGates(differences, dotnetTrainingGet);
        AddPreferenceGetGates(differences, railsResearchGet);
        AddPreferenceGetGates(differences, dotnetResearchGet);
        AddPreferencePostGates(differences, railsTrainingPost);
        AddPreferencePostGates(differences, dotnetTrainingPost);
        AddPreferencePostGates(differences, railsResearchPost);
        AddPreferencePostGates(differences, dotnetResearchPost);

        if (railsTrainingGet.SelectedValue != dotnetTrainingGet.SelectedValue)
            differences.Add($"training GET selection '{railsTrainingGet.SelectedValue}' != '{dotnetTrainingGet.SelectedValue}'");
        if (railsResearchGet.SelectedValue != dotnetResearchGet.SelectedValue)
            differences.Add($"research GET selection '{railsResearchGet.SelectedValue}' != '{dotnetResearchGet.SelectedValue}'");
        if (railsTrainingPost.FinalPath != dotnetTrainingPost.FinalPath)
            differences.Add($"training final path '{railsTrainingPost.FinalPath}' != '{dotnetTrainingPost.FinalPath}'");
        if (railsResearchPost.FinalPath != dotnetResearchPost.FinalPath)
            differences.Add($"research final path '{railsResearchPost.FinalPath}' != '{dotnetResearchPost.FinalPath}'");
        if (railsAccount.Semantics.TrainingEmailPreference != dotnetAccount.Semantics.TrainingEmailPreference)
            differences.Add($"final training preference '{railsAccount.Semantics.TrainingEmailPreference}' != '{dotnetAccount.Semantics.TrainingEmailPreference}'");
        if (railsAccount.Semantics.ResearchPreference != dotnetAccount.Semantics.ResearchPreference)
            differences.Add($"final research preference '{railsAccount.Semantics.ResearchPreference}' != '{dotnetAccount.Semantics.ResearchPreference}'");
        return differences.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void AddPreferenceGetGates(List<string> differences, PreferenceGetCapture capture)
    {
        if (capture.Status != 200)
            differences.Add($"{capture.App} {capture.Preference} GET status={capture.Status}; expected 200.");
        if (string.IsNullOrWhiteSpace(capture.FormAction) || string.IsNullOrWhiteSpace(capture.Token))
            differences.Add($"{capture.App} {capture.Preference} GET did not expose its form action and CSRF token.");
        if (capture.SelectedValue != "true")
            differences.Add($"{capture.App} {capture.Preference} GET selected '{capture.SelectedValue}'; expected true from the isolated fixture.");
    }

    private static void AddPreferencePostGates(List<string> differences, PreferenceSubmissionCapture capture)
    {
        if (capture.SubmissionStatus is not >= 300 or not < 400)
            differences.Add($"{capture.App} {capture.Preference} submission status={capture.SubmissionStatus}; expected a redirect.");
        if (capture.FinalStatus != 200 || capture.FinalPath != "/my-account")
            differences.Add($"{capture.App} {capture.Preference} final status={capture.FinalStatus} path={capture.FinalPath}; expected 200 at /my-account.");
        if (capture.FinalBody.Contains("account-preferences@example.test", StringComparison.OrdinalIgnoreCase))
            differences.Add($"{capture.App} {capture.Preference} final page leaked the fixture email.");
    }

    private static PreferenceFormCapture FindPreferenceForm(string body, string preference)
    {
        var expectedField = preference == "training" ? "training_emails" : "research_participant";
        var fieldMarkers = PreferenceFieldNames(expectedField)
            .Select(name => $@"name=""{name}""")
            .ToArray();
        var fieldIndex = fieldMarkers
            .Select(marker => body.IndexOf(marker, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (fieldIndex >= 0)
        {
            var formStart = body.LastIndexOf("<form", fieldIndex, StringComparison.OrdinalIgnoreCase);
            var formEnd = body.IndexOf("</form>", fieldIndex, StringComparison.OrdinalIgnoreCase);
            var headerEnd = body.IndexOf('>', formStart);
            if (formStart >= 0 && formEnd > formStart && headerEnd > formStart)
            {
                var header = body[formStart..(headerEnd + 1)];
                var formBody = body[formStart..(formEnd + "</form>".Length)];
                var field = CheckedPreferenceInput(formBody, expectedField)
                    ?? FirstPreferenceInput(formBody, expectedField);
                if (field is null)
                {
                    return new(null, "post", null, null, null, null);
                }

                var tokenName = FindInputName(formBody, "__RequestVerificationToken")
                    ?? FindInputName(formBody, "authenticity_token");
                return new(
                    AttributeValue(header, "action"),
                    AttributeValue(header, "method") ?? "post",
                    tokenName,
                    tokenName is null ? null : FindInputValue(formBody, tokenName),
                    field.Value.Name,
                    field.Value.Value);
            }
        }

        return new(null, "post", null, null, null, null);
    }

    private static string? AttributeValue(string tag, string attribute)
    {
        foreach (var quote in new[] { '"', '\'' })
        {
            var marker = $"{attribute}={quote}";
            var start = tag.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                continue;
            }

            start += marker.Length;
            var end = tag.IndexOf(quote, start);
            if (end >= 0)
            {
                return WebUtility.HtmlDecode(tag[start..end]);
            }
        }

        return null;
    }

    private static string FirstFormEvidence(string body)
    {
        var start = body.IndexOf("<form", StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return "no <form> element";
        }

        var end = body.IndexOf('>', start);
        return end > start ? body[start..(end + 1)] : body[start..];
    }

    private static string PreferenceEvidence(string body, string preference)
    {
        var marker = preference == "training" ? "TrainingEmails" : "ResearchParticipant";
        var index = body.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return $"no {marker} marker; body length={body.Length}";
        }

        var start = Math.Max(0, index - 160);
        var length = Math.Min(body.Length - start, 520);
        return body[start..(start + length)];
    }

    private static (string Name, string Value)? CheckedPreferenceInput(string body, string expectedField)
    {
        foreach (Match match in CheckedInputRegex().Matches(body))
        {
            var name = WebUtility.HtmlDecode(match.Groups["name"].Value);
            if (MatchesPreferenceField(name, expectedField))
            {
                return (name, WebUtility.HtmlDecode(match.Groups["value"].Value));
            }
        }

        return null;
    }

    private static (string Name, string Value)? FirstPreferenceInput(string body, string expectedField)
    {
        foreach (Match match in InputRegex().Matches(body))
        {
            var name = WebUtility.HtmlDecode(match.Groups["name"].Value);
            if (MatchesPreferenceField(name, expectedField))
            {
                return (name, WebUtility.HtmlDecode(match.Groups["value"].Value));
            }
        }

        return null;
    }

    private static string[] PreferenceFieldNames(string expectedField) =>
        [
            expectedField,
            $"user[{expectedField}]",
            expectedField == "training_emails" ? "TrainingEmails" : "ResearchParticipant",
        ];

    private static bool MatchesPreferenceField(string name, string expectedField) =>
        PreferenceFieldNames(expectedField).Contains(name, StringComparer.Ordinal);

    private static async Task WritePreferenceReportAsync(
        PreferenceGetCapture railsTrainingGet,
        PreferenceGetCapture dotnetTrainingGet,
        PreferenceSubmissionCapture railsTrainingPost,
        PreferenceSubmissionCapture dotnetTrainingPost,
        PreferenceGetCapture railsResearchGet,
        PreferenceGetCapture dotnetResearchGet,
        PreferenceSubmissionCapture railsResearchPost,
        PreferenceSubmissionCapture dotnetResearchPost,
        Capture railsAccount,
        Capture dotnetAccount,
        List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-account-preferences-parity.json"),
            JsonSerializer.Serialize(
                new
                {
                    scenario = "authenticated-account-preferences",
                    risk = "P1",
                    expectedIdentity = new { email = AccountPreferencesEmail, sub = AccountPreferencesSub, name = AccountPreferencesName },
                    rails = new
                    {
                        trainingGet = PreferenceGetReport(railsTrainingGet),
                        trainingPost = PreferenceSubmissionReport(railsTrainingPost),
                        researchGet = PreferenceGetReport(railsResearchGet),
                        researchPost = PreferenceSubmissionReport(railsResearchPost),
                        account = railsAccount.Semantics,
                    },
                    dotnet = new
                    {
                        trainingGet = PreferenceGetReport(dotnetTrainingGet),
                        trainingPost = PreferenceSubmissionReport(dotnetTrainingPost),
                        researchGet = PreferenceGetReport(dotnetResearchGet),
                        researchPost = PreferenceSubmissionReport(dotnetResearchPost),
                        account = dotnetAccount.Semantics,
                    },
                    acceptedNormalizations = new[]
                    {
                        "antiforgery token names and values",
                        "session and visit cookie names",
                        "generated IDs and timestamps",
                        "HTML whitespace and framework markup",
                        "Rails nested user[...] parameters and hidden PATCH method versus .NET flat POST form",
                        "named preference event rows and properties are reconciled by parity/reconcile.ps1",
                    },
                    differences,
                },
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object PreferenceGetReport(PreferenceGetCapture capture) => new
    {
        capture.App,
        capture.Preference,
        capture.Status,
        capture.Path,
        capture.FormAction,
        capture.Method,
        capture.TokenName,
        HasToken = !string.IsNullOrWhiteSpace(capture.Token),
        capture.FieldName,
        capture.SelectedValue,
    };

    private static object PreferenceSubmissionReport(PreferenceSubmissionCapture capture) => new
    {
        capture.App,
        capture.Preference,
        capture.SubmissionStatus,
        capture.InitialRedirect,
        capture.FinalStatus,
        capture.FinalPath,
        capture.FinalHeading,
    };

    [GeneratedRegex(@"<input\b(?=[^>]*\bname=""(?<name>[^""]+)"")(?=[^>]*\bvalue=""(?<value>[^""]*)"")(?=[^>]*\bchecked(?:\s*=\s*(?:""checked""|'checked'))?)[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CheckedInputRegex();

    private sealed record PreferenceFormCapture(
        string? Action,
        string Method,
        string? TokenName,
        string? Token,
        string? FieldName,
        string? SelectedValue);

    private sealed record PreferenceGetCapture(
        string App,
        string Preference,
        int Status,
        string Path,
        string? FormAction,
        string Method,
        string? TokenName,
        string? Token,
        string? FieldName,
        string? SelectedValue,
        string Body);

    private sealed record PreferenceSubmissionCapture(
        string App,
        string Preference,
        int SubmissionStatus,
        string? InitialRedirect,
        int FinalStatus,
        string FinalPath,
        string? FinalHeading,
        string FinalBody);
}
