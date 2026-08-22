using System.Net;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Rails v1.5.0 ac546721 learning-log contract from
/// Training::NotesController and app/views/notes: an authenticated learner can
/// view the log, POST an upserted note, PATCH it, and see the updated body.
/// </summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string LearningLogEmail = "assessment@example.test";
    private const string LearningLogSub = "synthetic-assessment";
    private const string LearningLogPath = "/my-account/learning-log";
    private const string LearningLogFormPath = "/modules/module-1/content-pages/key-concepts";
    private const string LearningLogModule = "module-1";
    private const string LearningLogNoteName = "key-concepts";
    private const string LearningLogNoteTitle = "Parity learning log note";
    private const string CreatedBody = "Parity learning log body: created.";
    private const string UpdatedBody = "Parity learning log body: updated.";

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_learning_log_create_and_update_semantics()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, LearningLogEmail, LearningLogSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsResult = await ExerciseLearningLogAsync(rails, "Rails");
        var dotnetResult = await ExerciseLearningLogAsync(dotnet, ".NET");
        var differences = CompareLearningLogs(railsResult, dotnetResult);

        await WriteLearningLogReportAsync(railsResult, dotnetResult, differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<LearningLogResult> ExerciseLearningLogAsync(IAPIRequestContext context, string app)
    {
        var initial = await CaptureLearningLogAsync(context, app, "initial");
        var createForm = await CaptureLearningLogFormAsync(context, app);
        var created = await SubmitLearningLogAsync(context, createForm, CreatedBody, false, app);
        var afterCreate = await CaptureLearningLogAsync(context, app, "after create");

        var updateForm = await CaptureLearningLogFormAsync(context, app);
        var updated = await SubmitLearningLogAsync(context, updateForm, UpdatedBody, true, app);
        var afterUpdate = await CaptureLearningLogAsync(context, app, "after update");
        var finalForm = await CaptureLearningLogFormAsync(context, app);

        return new(app, initial, createForm.Shape, created, afterCreate, updateForm.Body, updated, afterUpdate, finalForm.Body);
    }

    private static async Task<LearningLogPage> CaptureLearningLogAsync(
        IAPIRequestContext context,
        string app,
        string stage)
    {
        var response = await FetchAsync(context, LearningLogPath, app);
        var body = await response.TextAsync();
        return new(
            stage,
            response.Status,
            SanitizePath(LearningLogPath),
            Extract(HeadingRegex(), body),
            body.Contains(CreatedBody, StringComparison.Ordinal),
            body.Contains(UpdatedBody, StringComparison.Ordinal),
            body.Contains(LearningLogNoteTitle, StringComparison.Ordinal));
    }

    private static async Task<LearningLogForm> CaptureLearningLogFormAsync(IAPIRequestContext context, string app)
    {
        var response = await FetchAsync(context, LearningLogFormPath, app);
        var body = await response.TextAsync();
        Ensure(response.Status == 200, $"{app} learning-log form GET status={response.Status}; expected 200 at {LearningLogFormPath}.");

        foreach (Match match in FormRegex().Matches(body))
        {
            var formBody = match.Value;
            if (!ContainsLearningLogBodyField(formBody))
            {
                continue;
            }

            var fields = InputRegex().Matches(formBody)
                .ToDictionary(
                    input => WebUtility.HtmlDecode(input.Groups["name"].Value),
                    input => WebUtility.HtmlDecode(input.Groups["value"].Value),
                    StringComparer.Ordinal);
            var tokenName = fields.Keys.FirstOrDefault(name =>
                name is "__RequestVerificationToken" or "authenticity_token");
            Ensure(tokenName is not null && !string.IsNullOrWhiteSpace(fields[tokenName]),
                $"{app} learning-log form has no CSRF token.");

            var prefix = fields.Keys.Any(name => name.StartsWith("note[", StringComparison.Ordinal)) ? "note" : null;
            var bodyField = prefix is null ? "Body" : "note[body]";
            var bodyValue = TextareaRegex().Matches(formBody)
                .FirstOrDefault(textarea => WebUtility.HtmlDecode(textarea.Groups["name"].Value) == bodyField)?
                .Groups["value"].Value;
            return new(
                SanitizePath(WebUtility.HtmlDecode(match.Groups["action"].Value)),
                tokenName!,
                fields[tokenName!],
                prefix,
                WebUtility.HtmlDecode(bodyValue ?? string.Empty).Trim(),
                new(response.Status, SanitizePath(LearningLogFormPath), prefix is not null));
        }

        Fail($"{app} {LearningLogFormPath} did not expose a learning-log form.");
        throw new UnreachableException();
    }

    private static async Task<LearningLogSubmission> SubmitLearningLogAsync(
        IAPIRequestContext context,
        LearningLogForm source,
        string body,
        bool update,
        string app)
    {
        string Field(string name) => source.Prefix is null ? name : $"{source.Prefix}[{ToRailsField(name)}]";
        var form = context.CreateFormData();
        form.Set(source.TokenName, source.Token);
        form.Set(Field("Title"), LearningLogNoteTitle);
        form.Set(Field("TrainingModule"), LearningLogModule);
        form.Set(Field("Name"), LearningLogNoteName);
        form.Set(Field("Body"), body);
        var isRails = app.Equals("Rails", StringComparison.Ordinal);
        if (update && isRails)
        {
            form.Set("_method", "patch");
        }

        var response = await context.FetchAsync(source.Action, new APIRequestContextOptions
        {
            Method = update && !isRails ? "PATCH" : "POST",
            MaxRedirects = 0,
            FailOnStatusCode = false,
            Form = form,
        });

        return new(response.Status, SanitizePath(LocationOf(response)));
    }

    private static List<string> CompareLearningLogs(LearningLogResult rails, LearningLogResult dotnet)
    {
        var differences = new List<string>();
        AddLearningLogGates(differences, rails);
        AddLearningLogGates(differences, dotnet);

        if (rails.Initial.Heading != dotnet.Initial.Heading)
            differences.Add($"learning-log heading '{rails.Initial.Heading}' != '{dotnet.Initial.Heading}'");
        if (rails.Create.Status != dotnet.Create.Status || rails.Create.Redirect != dotnet.Create.Redirect)
            differences.Add($"create result Rails {rails.Create.Status} {rails.Create.Redirect} != .NET {dotnet.Create.Status} {dotnet.Create.Redirect}");
        if (rails.Update.Status != dotnet.Update.Status || rails.Update.Redirect != dotnet.Update.Redirect)
            differences.Add($"update result Rails {rails.Update.Status} {rails.Update.Redirect} != .NET {dotnet.Update.Status} {dotnet.Update.Redirect}");

        return differences.Distinct(StringComparer.Ordinal).ToList();
    }

    private static void AddLearningLogGates(List<string> differences, LearningLogResult result)
    {
        if (result.Initial.Status != 200 || result.Initial.Heading != "Your learning log")
            differences.Add($"{result.App} initial GET was {result.Initial.Status} heading '{result.Initial.Heading}'; expected learning log 200.");
        if (result.Form.Status != 200)
            differences.Add($"{result.App} note-form GET was {result.Form.Status}; expected 200.");
        if (result.Create.Status is not >= 300 or not < 400)
            differences.Add($"{result.App} create status={result.Create.Status}; expected redirect.");
        if (result.CreatedBodyObserved != CreatedBody)
            differences.Add($"{result.App} note form did not show the deterministically created body.");
        if (result.Update.Status is not >= 300 or not < 400)
            differences.Add($"{result.App} update status={result.Update.Status}; expected redirect.");
        if (result.UpdatedBodyObserved != UpdatedBody)
            differences.Add($"{result.App} note form did not replace the created body with the updated body.");
    }

    private static bool ContainsLearningLogBodyField(string formBody) =>
        formBody.Contains("name=\"Body\"", StringComparison.Ordinal)
        || formBody.Contains("name=\"note[body]\"", StringComparison.Ordinal);

    private static string ToRailsField(string name) => name switch
    {
        "TrainingModule" => "training_module",
        _ => name.ToLowerInvariant(),
    };

    private static async Task WriteLearningLogReportAsync(
        LearningLogResult rails,
        LearningLogResult dotnet,
        List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-learning-log-parity.json"),
            JsonSerializer.Serialize(new
            {
                scenario = "authenticated-learning-log-create-update",
                risk = "P1",
                expectedIdentity = new { email = LearningLogEmail, sub = LearningLogSub },
                note = new { trainingModule = LearningLogModule, name = LearningLogNoteName, title = LearningLogNoteTitle },
                rails,
                dotnet,
                acceptedNormalizations = new[]
                {
                    "antiforgery token names and values",
                    "Rails nested note[...] fields versus .NET flat fields",
                    "framework form markup and session cookies",
                    "generated note/event IDs and timestamps",
                    "note and event database state is left for parity/reconcile.ps1",
                },
                differences,
            }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed record LearningLogForm(
        string Action,
        string TokenName,
        string Token,
        string? Prefix,
        string Body,
        LearningLogFormShape Shape);
    private sealed record LearningLogFormShape(int Status, string Path, bool UsesNestedRailsFields);
    private sealed record LearningLogPage(
        string Stage,
        int Status,
        string Path,
        string? Heading,
        bool HasCreatedBody,
        bool HasUpdatedBody,
        bool HasTitle);
    private sealed record LearningLogSubmission(int Status, string Redirect);
    private sealed record LearningLogResult(
        string App,
        LearningLogPage Initial,
        LearningLogFormShape Form,
        LearningLogSubmission Create,
        LearningLogPage AfterCreate,
        string CreatedBodyObserved,
        LearningLogSubmission Update,
        LearningLogPage AfterUpdate,
        string UpdatedBodyObserved);

    [GeneratedRegex("<textarea\\b(?=[^>]*\\bname=\"(?<name>[^\"]+)\")[^>]*>(?<value>.*?)</textarea>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TextareaRegex();
}
