using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>Rails v1.5.0 CloseAccountsController and User#redact! contract.</summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string ClosureEmail = "account-closure@example.test";
    private const string ClosureSub = "synthetic-account-closure";
    private const string ClosureReasonPath = "/my-account/close/edit-reason";
    private const string ClosureInternalMailbox = "child-development.training@education.gov.uk";
    private const string AccountClosedTemplate = "0a4754ee-6175-444c-98a1-ebef0b14e7f7";
    private const string AccountClosedInternalTemplate = "a2dba0ef-84f1-4b4d-b50a-ce953050798e";

    [ParityFact]
    public async Task Rails_and_dotnet_close_and_redact_the_same_populated_account()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var notify = await playwright.APIRequest.NewContextAsync(new() { BaseURL = "http://localhost:4010" });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, ClosureEmail, ClosureSub);
        await SignInRailsAsync(rails);
        var railsResult = await ExerciseClosureAsync(rails, notify, "Rails");

        await ConfigureSimulatorAsync(simulator, ClosureEmail, ClosureSub);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");
        var dotnetResult = await ExerciseClosureAsync(dotnet, notify, ".NET");

        Assert.Equal(railsResult, dotnetResult);
    }

    private static async Task<ClosureCapture> ExerciseClosureAsync(IAPIRequestContext context, IAPIRequestContext notify, string app)
    {
        var initial = await GetPageAsync(context, ClosureReasonPath, app);
        Ensure(initial.Status == 200 && initial.Heading.Contains("why", StringComparison.OrdinalIgnoreCase),
            $"{app} closure reason page was not rendered.");

        var blank = await SubmitClosureAsync(context, initial, new()
        {
            ["ClosedReason"] = "", ["user[closed_reason]"] = "",
        }, app);
        Ensure(blank.Body.Contains("Select a reason", StringComparison.Ordinal),
            $"{app} blank closure reason returned status={blank.Status} without the Rails validation message.");

        var blankOther = await GetPageAsync(context, ClosureReasonPath, app);
        blankOther = await SubmitClosureAsync(context, blankOther, new()
        {
            ["ClosedReason"] = "other", ["user[closed_reason]"] = "other",
            ["ClosedReasonCustom"] = "", ["user[closed_reason_custom]"] = "",
        }, app);
        Ensure(blankOther.Status is >= 300 and < 400 && SanitizePath(blankOther.Location) == "/my-account/close/confirm",
            $"{app} blank custom reason must normalize to 'No reason provided' and redirect to confirmation.");

        var reason = await GetPageAsync(context, ClosureReasonPath, app);
        var reasonPost = await SubmitClosureAsync(context, reason, new()
        {
            ["ClosedReason"] = "other", ["user[closed_reason]"] = "other",
            ["ClosedReasonCustom"] = "Parity closure reason", ["user[closed_reason_custom]"] = "Parity closure reason",
        }, app);
        Ensure(reasonPost.Status is >= 300 and < 400 && SanitizePath(reasonPost.Location) == "/my-account/close/confirm",
            $"{app} valid custom reason did not redirect to confirmation.");

        var confirm = await GetPageAsync(context, "/my-account/close/confirm", app);
        Ensure(confirm.Status == 200 && confirm.Body.Contains("Close my account", StringComparison.Ordinal) &&
               confirm.Body.Contains("Cancel and go back to my account", StringComparison.Ordinal),
            $"{app} confirmation semantics differ.");

        var requestsBefore = await NotifyRequestsAsync(notify);
        var close = await SubmitClosureAsync(context, confirm, new(), app, closeForm: true);
        Ensure(close.Status is >= 300 and < 400 && SanitizePath(close.Location) == "/my-account/close",
            $"{app} close action returned status={close.Status}, location='{close.Location}'; expected redirect to the public confirmation.");
        var closed = await GetPageAsync(context, close.Location!, app);
        Ensure(closed.Status == 200 && closed.Body.Contains("Account closed", StringComparison.Ordinal),
            $"{app} public closed-account page was not shown.");

        var protectedPage = await FetchAsync(context, "/my-account", app);
        Ensure(protectedPage.Status is >= 300 and < 400,
            $"{app} retained an authenticated session after closure (status {protectedPage.Status}).");

        var notifications = (await NotifyRequestsAsync(notify)).Skip(requestsBefore.Count).Select(ParseNotify).ToArray();
        if (app == "Rails")
        {
            Ensure(notifications.Length == 0,
                "Rails development NotifyDelivery unexpectedly used the HTTP recording fake; it should log both emails locally.");
        }
        else
        {
            Ensure(notifications.Length == 2, $"{app} emitted {notifications.Length} closure Notify calls; expected exactly 2.");
            Ensure(notifications.Any(x => x.Template == AccountClosedTemplate && x.Recipient == ClosureEmail),
                $"{app} did not send the learner closure template to the original email.");
            Ensure(notifications.Any(x => x.Template == AccountClosedInternalTemplate && x.Recipient == ClosureInternalMailbox),
                $"{app} did not send the internal closure template to the configured mailbox.");
        }

        return new(blank.Status, blankOther.Status, SanitizePath(reasonPost.Location), SanitizePath(close.Location),
            closed.Status, SanitizePath(LocationOf(protectedPage)), 2);
    }

    private static async Task<ClosurePage> GetPageAsync(IAPIRequestContext context, string path, string app)
    {
        var response = await FetchAsync(context, path, app);
        var body = await response.TextAsync();
        return new(response.Status, body, Extract(HeadingRegex(), body) ?? string.Empty, LocationOf(response));
    }

    private static async Task<ClosurePage> SubmitClosureAsync(IAPIRequestContext context, ClosurePage page,
        Dictionary<string, string> values, string app, bool closeForm = false)
    {
        var forms = ClosureFormRegex().Matches(page.Body).Cast<Match>();
        var form = forms.FirstOrDefault(x => closeForm
            ? x.Value.Contains("close_account", StringComparison.OrdinalIgnoreCase)
            : x.Value.Contains("update-reason", StringComparison.OrdinalIgnoreCase));
        Ensure(form is not null, $"{app} closure form was not found.");
        var action = WebUtility.HtmlDecode(FormActionRegex().Match(form!.Value).Groups["action"].Value);
        var fields = context.CreateFormData();
        foreach (Match input in InputRegex().Matches(form.Value))
        {
            var name = WebUtility.HtmlDecode(input.Groups["name"].Value);
            if (!string.IsNullOrEmpty(name) && input.Value.Contains("type=\"hidden\"", StringComparison.OrdinalIgnoreCase))
                fields.Set(name, WebUtility.HtmlDecode(input.Groups["value"].Value));
        }
        foreach (var value in values) fields.Set(value.Key, value.Value);
        var response = await context.PostAsync(action, new() { Form = fields, FailOnStatusCode = false, MaxRedirects = 0 });
        var body = await response.TextAsync();
        return new(response.Status, body, Extract(HeadingRegex(), body) ?? string.Empty, LocationOf(response));
    }

    private static async Task<List<JsonElement>> NotifyRequestsAsync(IAPIRequestContext notify)
    {
        var response = await notify.GetAsync("/_requests", new() { FailOnStatusCode = false });
        Ensure(response.Status == 200, $"Notify fake request log returned {response.Status}.");
        using var document = JsonDocument.Parse(await response.TextAsync());
        return document.RootElement.EnumerateArray().Select(x => x.Clone()).ToList();
    }

    private static NotifyCapture ParseNotify(JsonElement row)
    {
        using var body = JsonDocument.Parse(row.GetProperty("body").GetString() ?? "{}");
        var root = body.RootElement;
        return new(root.GetProperty("template_id").GetString() ?? "", root.GetProperty("email_address").GetString() ?? "");
    }

    private sealed record ClosurePage(int Status, string Body, string Heading, string? Location);
    private sealed record NotifyCapture(string Template, string Recipient);
    private sealed record ClosureCapture(int BlankStatus, int BlankOtherStatus, string ConfirmPath, string ClosedPath,
        int ClosedStatus, string SignedOutRedirect, int NotifyCount);

    [GeneratedRegex("<form\\b(?<attributes>[^>]*)>(?<body>.*?)</form>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ClosureFormRegex();

    [GeneratedRegex("\\baction=[\\\"'](?<action>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase)]
    private static partial Regex FormActionRegex();
}
