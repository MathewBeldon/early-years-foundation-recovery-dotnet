using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Authenticated GET /my-account parity against live Rails v1.5.0 commit ac546721
/// and .NET for the existing synthetic learner (existing@example.test / synthetic-existing).
///
/// Rails v1.5.0 (ac5467218a49c9de58a32a69d4edc01ce37710cf):
/// <c>User.find_or_create_from_gov_one</c> in <c>app/models/user.rb</c>,
/// <c>Users::OmniauthCallbacksController#openid_connect</c> in
/// <c>app/controllers/users/omniauth_callbacks_controller.rb</c>,
/// <c>ApplicationController#authenticate_registered_user!</c>,
/// <c>UserController#show</c>, <c>User#name</c>, and route <c>/my-account</c>
/// (<c>resource :user, controller: :user, only: %i[show], path: 'my-account'</c>).
///
/// .NET mirrors lookup in <c>UserRepository.FindOrCreateFromGovOneAsync</c> and
/// <c>UserController.Show</c>. This file is one scenario, not a generalized auth framework.
/// </summary>
[Trait("Category", "Parity")]
public sealed partial class AuthenticatedAccountParityTests
{
    private const string SimulatorBaseUrl = "http://localhost:3333";
    private const string ContainerSimulatorHost = "gov-one-login-simulator:3000";
    private const string HostSimulatorHost = "localhost:3333";
    private const string ExpectedEmail = "existing@example.test";
    private const string ExpectedSub = "synthetic-existing";
    private const string ExpectedName = "Synthetic Learner";
    private const string RejectedEmail = "new@example.test";
    private const string RailsCallbackPath = "/users/auth/openid_connect/callback";
    private const string LogoutPath = "/users/sign_out";
    private const string NameKey = "Name";
    private const string TrainingEmailKey = "Email updates about this training course";
    private const string ResearchKey = "Willing to speak to a researcher to improve this service";
    private const string SimulatorMustRun = "Run ./parity.ps1 up or reset; simulator must be on :3333.";
    private const string PreserveUrlLists = "Copy GET lists; never send a single-app redirectUrls. Do not authenticate.";
    private const string PreserveClientId = "Preserve the existing clientId from GET /config.";
    private const int MaxAuthHops = 8;

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_authenticated_account_semantics()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator);
        await SignInRailsAsync(rails);
        var railsCapture = await CaptureMyAccountAsync(rails, "Rails");
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");
        var dotnetCapture = await CaptureMyAccountAsync(dotnet, ".NET");

        var differences = Differences(railsCapture, dotnetCapture);
        await WriteReportAsync(railsCapture.Semantics, dotnetCapture.Semantics, differences);
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task ConfigureSimulatorAsync(IAPIRequestContext simulator)
    {
        var before = await ReadConfigAsync(simulator, "before identity merge");
        AssertBothAppsUrlLists(before, "before identity merge");

        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["clientConfiguration"] = new Dictionary<string, object?>
            {
                ["clientId"] = before.ClientId,
                ["redirectUrls"] = before.RedirectUrls,
                ["postLogoutRedirectUrls"] = before.PostLogoutRedirectUrls,
            },
            ["responseConfiguration"] = new Dictionary<string, object?>
            {
                ["email"] = ExpectedEmail,
                ["sub"] = ExpectedSub,
            },
        });
        var post = await FetchAsync(simulator, "/config", "Simulator", data: payload);
        Ensure(post.Status == 200, $"Simulator status={post.Status} path=/config. POST identity merge expected 200. {SimulatorMustRun}");

        var after = await ReadConfigAsync(simulator, "after identity merge");
        Ensure(
            after.Email == ExpectedEmail && after.Sub == ExpectedSub,
            $"Simulator status=200 path=/config. Identity readback expected {ExpectedEmail} / {ExpectedSub}. " +
            "POST a partial merge of responseConfiguration only; GET-verify before authenticating.");
        Ensure(
            after.ClientId == before.ClientId,
            $"Simulator status=200 path=/config. clientId after POST was not the pre-POST value. {PreserveClientId} Do not authenticate.");
        AssertBothAppsUrlLists(after, "after identity merge");
        Ensure(
            after.RedirectUrls.Count >= before.RedirectUrls.Count &&
            after.PostLogoutRedirectUrls.Count >= before.PostLogoutRedirectUrls.Count,
            $"Simulator status=200 path=/config. URL lists shrank after POST " +
            $"(redirects {before.RedirectUrls.Count}->{after.RedirectUrls.Count}, " +
            $"logouts {before.PostLogoutRedirectUrls.Count}->{after.PostLogoutRedirectUrls.Count}). {PreserveUrlLists}");
    }

    private static async Task<SimulatorConfig> ReadConfigAsync(IAPIRequestContext simulator, string stage)
    {
        var response = await FetchAsync(simulator, "/config", "Simulator", stage: stage);
        Ensure(response.Status == 200, $"{SimulatorEvidence(response.Status, stage)}. Expected 200. {SimulatorMustRun}");

        using var document = JsonDocument.Parse(await response.TextAsync());
        var root = document.RootElement;
        if (!root.TryGetProperty("clientConfiguration", out var client))
            Fail($"{SimulatorEvidence(200, stage)}. Missing clientConfiguration. Run ./parity.ps1 up; simulator must be on :3333.");

        string? email = null, sub = null;
        if (root.TryGetProperty("responseConfiguration", out var responseConfiguration))
        {
            email = OptionalString(responseConfiguration, "email");
            sub = OptionalString(responseConfiguration, "sub");
        }

        return new(
            RequiredString(client, "clientId", stage),
            RequiredUrlList(client, "redirectUrls", stage),
            RequiredUrlList(client, "postLogoutRedirectUrls", stage),
            email,
            sub);
    }

    private static void AssertBothAppsUrlLists(SimulatorConfig config, string stage)
    {
        var railsCallback = HasHostPath(config.RedirectUrls, 3000, RailsCallbackPath);
        var dotnetCallback = HasHostPath(config.RedirectUrls, 5000, RailsCallbackPath);
        var railsLogout = HasHostPath(config.PostLogoutRedirectUrls, 3000, LogoutPath);
        var dotnetLogout = HasHostPath(config.PostLogoutRedirectUrls, 5000, LogoutPath);
        Ensure(
            railsCallback && dotnetCallback && railsLogout && dotnetLogout,
            $"{SimulatorEvidence(200, stage)}. Both apps' callback/logout paths were not preserved " +
            $"(redirects={config.RedirectUrls.Count}, logouts={config.PostLogoutRedirectUrls.Count}; " +
            $"Rails callback={railsCallback}, .NET callback={dotnetCallback}, " +
            $"Rails logout={railsLogout}, .NET logout={dotnetLogout}). {PreserveUrlLists}");
    }

    private static async Task SignInRailsAsync(IAPIRequestContext rails)
    {
        var signIn = await FetchAsync(rails, "/users/sign-in", "Rails");
        Ensure(
            signIn.Status == 200,
            $"Rails status={signIn.Status} path=/users/sign-in. Expected 200 so login_uri can plant session " +
            "and emit the Continue href. One GET /users/sign-in; use that response's href.");

        var matches = ContinueHrefRegex().Matches(await signIn.TextAsync());
        Ensure(
            matches.Count == 1,
            $"Rails status=200 path=/users/sign-in Continue href count={matches.Count} (expected 1). " +
            "One GET /users/sign-in; use that response's href. A second GET rotates session and invalidates the href.");
        var href = WebUtility.HtmlDecode(matches[0].Groups["href"].Value);
        var path = SanitizePath(href);
        Ensure(
            path.Equals("/authorize", StringComparison.OrdinalIgnoreCase),
            $"Rails status=200 path=/users/sign-in Continue href path={path}. Expected /authorize. " +
            "Do not start Rails at /users/auth/openid_connect; parse the Continue href from that sign-in response.");
        await FollowAuthHopsAsync(rails, RewriteSimulatorHost(href), "Rails");
    }

    private static async Task FollowAuthHopsAsync(IAPIRequestContext context, string startUrl, string app)
    {
        var current = startUrl;
        for (var hop = 0; hop < MaxAuthHops; hop++)
        {
            current = RewriteSimulatorHost(current);
            var response = await FetchAsync(context, current, app);
            var path = SanitizePath(current);
            if (response.Status is >= 300 and < 400)
            {
                var location = LocationOf(response);
                Ensure(
                    !string.IsNullOrWhiteSpace(location),
                    $"{app} status={response.Status} path={path} missing Location during auth bootstrap. {Remediation(app, response.Status, path)}");
                current = RewriteSimulatorHost(WebUtility.HtmlDecode(location!));
                continue;
            }

            if (response.Status == 200)
            {
                Ensure(
                    !path.Equals("/authorize", StringComparison.OrdinalIgnoreCase),
                    $"{app} status=200 path=/authorize. Interactive simulator form. {Remediation(app, 200, path)}");
                return;
            }

            Fail($"{app} status={response.Status} path={path} during auth bootstrap. {Remediation(app, response.Status, path)}");
        }

        Fail($"{app} status=<too-many-redirects> path={SanitizePath(current)} exceeded {MaxAuthHops} hops. {Remediation(app, 0, SanitizePath(current))}");
    }

    private static async Task<Capture> CaptureMyAccountAsync(IAPIRequestContext context, string app)
    {
        var response = await FetchAsync(context, "/my-account", app);
        var body = await response.TextAsync();
        response.Headers.TryGetValue("content-type", out var contentType);
        var location = LocationOf(response);
        var redirect = response.Status is >= 300 and < 400 ? SanitizePath(location) : null;
        var rows = SummaryRows(body);
        var nameFound = rows.TryGetValue(NameKey, out var name);
        var trainingFound = rows.TryGetValue(TrainingEmailKey, out var training);
        var researchFound = rows.TryGetValue(ResearchKey, out var research);
        return new(
            app,
            new AccountSemanticResult(
                response.Status, redirect ?? "/my-account", redirect, Extract(HeadingRegex(), body),
                name, training, research, ExtractAll(ValidationRegex(), body), ExtractAll(NavigationRegex(), body), contentType),
            body.Contains(RejectedEmail, StringComparison.OrdinalIgnoreCase), nameFound, trainingFound, researchFound);
    }

    private static List<string> Differences(Capture rails, Capture dotnet)
    {
        var differences = new List<string>();
        differences.AddRange(Gates(rails));
        differences.AddRange(Gates(dotnet));
        var a = rails.Semantics;
        var b = dotnet.Semantics;
        if (a.Status != b.Status) differences.Add(CompareEvidence(a, b, $"status {a.Status} != {b.Status}"));
        if (a.Redirect != b.Redirect) differences.Add(CompareEvidence(a, b, $"redirect '{a.Redirect}' != '{b.Redirect}'"));
        if (a.Heading != b.Heading) differences.Add(CompareEvidence(a, b, $"heading '{a.Heading}' != '{b.Heading}'"));
        if (a.Name != b.Name) differences.Add(CompareEvidence(a, b, $"name '{a.Name}' != '{b.Name}'"));
        if (rails.TrainingFound && dotnet.TrainingFound && a.TrainingEmailPreference != b.TrainingEmailPreference)
            differences.Add(CompareEvidence(a, b, $"training-email preference '{a.TrainingEmailPreference}' != '{b.TrainingEmailPreference}'"));
        if (rails.ResearchFound && dotnet.ResearchFound && a.ResearchPreference != b.ResearchPreference)
            differences.Add(CompareEvidence(a, b, $"research preference '{a.ResearchPreference}' != '{b.ResearchPreference}'"));
        if (!a.ValidationMessages.SequenceEqual(b.ValidationMessages)) differences.Add(CompareEvidence(a, b, "validation messages differ"));
        if (!a.Navigation.SequenceEqual(b.Navigation)) differences.Add(CompareEvidence(a, b, "signed-in navigation differs"));
        if (NormalizeMediaType(a.ContentType) != NormalizeMediaType(b.ContentType)) differences.Add(CompareEvidence(a, b, "content type differs"));
        return differences.Distinct(StringComparer.Ordinal).ToList();
    }

    private static List<string> Gates(Capture capture)
    {
        var result = capture.Semantics;
        var evidence = $"{capture.App} status={result.Status} path={result.Path} redirect={result.Redirect ?? "none"}";
        var preferenceEvidence = $"{capture.App} status={result.Status} path={result.Path}";
        var failures = new List<string>();
        if (result.Status != 200 || result.Redirect is not null || result.Path != "/my-account")
            failures.Add($"{evidence}: expected 200, no redirect, final path /my-account. {Remediation(capture.App, result.Status, result.Path)}");
        if (result.Heading != "Manage your account")
            failures.Add($"{evidence}: heading '{result.Heading}' != 'Manage your account'. {Remediation(capture.App, result.Status, result.Path)}");
        if (!capture.NameFound)
        {
            failures.Add(
                $"{evidence}: Name summary row was not found. Page markup did not support named dt.govuk-summary-list__key extraction. " +
                "Stopped rather than inventing a broader regex. Expected Name 'Synthetic Learner' for " +
                $"{ExpectedEmail} / {ExpectedSub}.");
        }
        else if (string.IsNullOrWhiteSpace(result.Name))
        {
            failures.Add(
                $"{evidence}: Name summary row was blank. Rejected. Expected '{ExpectedName}' for {ExpectedEmail} / {ExpectedSub}. " +
                Remediation(capture.App, result.Status, result.Path));
        }
        else if (result.Name != ExpectedName)
        {
            failures.Add(
                $"{evidence}: Name '{result.Name}' != '{ExpectedName}'. Simulator readback plus this displayed name is the identity gate " +
                $"for {ExpectedEmail} / {ExpectedSub}. {Remediation(capture.App, result.Status, result.Path)}");
        }

        if (capture.ContainsRejectedEmail)
            failures.Add($"{evidence}: page contained {RejectedEmail}. Rejected. Expected existing synthetic learner {ExpectedEmail} / {ExpectedSub}.");
        if (!capture.TrainingFound)
        {
            failures.Add(
                $"{preferenceEvidence}: named summary-list key '{TrainingEmailKey}' was not found. " +
                "Stopped rather than inventing a broader regex.");
        }

        if (!capture.ResearchFound)
        {
            failures.Add(
                $"{preferenceEvidence}: named summary-list key '{ResearchKey}' was not found. " +
                "Stopped rather than inventing a broader regex.");
        }

        return failures;
    }

    private static string CompareEvidence(AccountSemanticResult rails, AccountSemanticResult dotnet, string difference) =>
        $"Rails status={rails.Status} path={rails.Path} redirect={rails.Redirect ?? "none"}; " +
        $".NET status={dotnet.Status} path={dotnet.Path} redirect={dotnet.Redirect ?? "none"}: {difference}. " +
        "Do not normalize this user-observable field or change app behavior; investigate the live pages.";

    private static async Task WriteReportAsync(AccountSemanticResult rails, AccountSemanticResult dotnet, List<string> differences)
    {
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(reportDirectory, "authenticated-account-parity.json"),
            JsonSerializer.Serialize(
                new AuthenticatedAccountParityReport(
                    "authenticated-account",
                    "P1",
                    new ExpectedIdentity(ExpectedEmail, ExpectedSub, ExpectedName),
                    rails,
                    dotnet,
                    ["antiforgery values", "session cookie names", "generated IDs", "timestamps", "HTML whitespace"],
                    differences),
                new JsonSerializerOptions { WriteIndented = true }));
    }

    private static async Task<IAPIResponse> FetchAsync(
        IAPIRequestContext context, string url, string app, string? data = null, string? stage = null)
    {
        try
        {
            var options = new APIRequestContextOptions { MaxRedirects = 0, FailOnStatusCode = false };
            if (data is not null)
            {
                options.Method = "POST";
                options.Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" };
                options.Data = data;
            }

            return await context.FetchAsync(url, options);
        }
        catch (Exception ex)
        {
            var path = SanitizePath(url);
            var message = SanitizeMessage(ex.Message);
            if (app == "Simulator")
            {
                var staged = stage is null ? "" : $" ({stage})";
                Fail($"Simulator status=<request-failed> path={path}{staged}. {message} {SimulatorMustRun}");
            }

            var missedRewrite = message.Contains("gov-one-login-simulator", StringComparison.OrdinalIgnoreCase)
                || url.Contains(ContainerSimulatorHost, StringComparison.Ordinal);
            Fail(
                $"{app} status=<request-failed> path={path}. {message} " +
                (missedRewrite
                    ? $"Rewrite only {ContainerSimulatorHost} to {HostSimulatorHost} for the host-side authorize hop."
                    : Remediation(app, 0, path)));
            throw;
        }
    }

    private static string RewriteSimulatorHost(string url) =>
        url.Replace(ContainerSimulatorHost, HostSimulatorHost, StringComparison.Ordinal);

    private static string? LocationOf(IAPIResponse response) =>
        response.Headers.FirstOrDefault(header => header.Key.Equals("location", StringComparison.OrdinalIgnoreCase)).Value;

    private static Dictionary<string, string> SummaryRows(string body)
    {
        var rows = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in SummaryRowRegex().Matches(body))
        {
            var key = VisibleText(match.Groups["key"].Value);
            if (key.Length > 0) rows.TryAdd(key, VisibleText(match.Groups["value"].Value));
        }

        return rows;
    }

    private static string? Extract(Regex regex, string body) =>
        regex.Match(body) is { Success: true } match ? VisibleText(match.Groups[1].Value) : null;

    private static List<string> ExtractAll(Regex regex, string body) => regex.Matches(body)
        .Select(x => VisibleText(x.Groups[1].Value))
        .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).ToList()!;

    private static string VisibleText(string value) =>
        NormalizeWhitespace(WebUtility.HtmlDecode(StripTags().Replace(value, " "))) ?? string.Empty;

    private static string? NormalizeWhitespace(string? value) =>
        value is null ? null : Whitespace().Replace(value, " ").Trim();

    private static string? NormalizeMediaType(string? value) => value?.Split(';', 2)[0].Trim().ToLowerInvariant();

    private static string SanitizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<none>";
        var text = value.Trim();
        if (Uri.TryCreate(text, UriKind.Absolute, out var absolute))
            return string.IsNullOrEmpty(absolute.AbsolutePath) ? "/" : absolute.AbsolutePath;
        if (!text.StartsWith('/') && text.Contains('/') && Uri.TryCreate("http://" + text, UriKind.Absolute, out var prefixed))
            return string.IsNullOrEmpty(prefixed.AbsolutePath) ? "/" : prefixed.AbsolutePath;
        var cut = text.IndexOfAny(['?', '#']);
        var path = cut >= 0 ? text[..cut] : text;
        return path.Length == 0 ? "/" : path;
    }

    private static string SanitizeMessage(string message)
    {
        var sanitized = AbsoluteUrlRegex().Replace(message, match => SanitizePath(match.Value));
        sanitized = RelativeQueryRegex().Replace(sanitized, match => SanitizePath(match.Value));
        sanitized = AuthHeaderRegex().Replace(sanitized, "${name}:<redacted>");
        return SecretValueRegex().Replace(sanitized, match =>
        {
            var name = match.Groups["name"].Value;
            if (match.Value.StartsWith('"')) return $"\"{name}\":\"<redacted>\"";
            return match.Value.Contains('=') ? $"{name}=<redacted>" : $"{name}:<redacted>";
        });
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition) Fail(message);
    }

    [DoesNotReturn]
    private static void Fail(string message) => Assert.Fail(SanitizeMessage(message));

    private static string Remediation(string app, int status, string path)
    {
        if (path.Equals("/authorize", StringComparison.OrdinalIgnoreCase) && status == 200)
            return "This stack must stay non-interactive; do not add form-fill.";
        if (status == 422 || (path.Equals(RailsCallbackPath, StringComparison.OrdinalIgnoreCase) && status >= 400))
        {
            return "Rails User.find_or_create_from_gov_one save! rejected the user. " +
                   "Apply the existing fixture patch (setting_type_id=other and terms) and rerun ./parity.ps1 reset when reseeding is allowed.";
        }

        if (path.Contains("terms-and-conditions", StringComparison.OrdinalIgnoreCase))
            return $"Third or blank account. Simulator sub/email must be {ExpectedSub} / {ExpectedEmail}.";
        if (path.Contains("sign-in", StringComparison.OrdinalIgnoreCase))
        {
            return app == "Rails"
                ? "Session cookie was not sent or the Continue href was stale. One GET /users/sign-in; use that response's href; do not reuse contexts across apps."
                : "Not signed in. Session cookie was not sent; do not reuse contexts across apps.";
        }

        if (status is >= 300 and < 400)
            return "Authenticated /my-account must not redirect. Check the identity gate and that each app used its own IAPIRequestContext.";
        return "Check the live parity stack on :3000/:5000 and the simulator on :3333; do not persist cookies or callback query.";
    }

    private static string SimulatorEvidence(int status, string stage) => $"Simulator status={status} path=/config ({stage})";

    private static bool HasHostPath(List<string> urls, int port, string path) => urls.Any(url =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) &&
        uri.Port == port &&
        uri.AbsolutePath.Equals(path, StringComparison.OrdinalIgnoreCase));

    private static string RequiredString(JsonElement parent, string name, string stage)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            Fail($"{SimulatorEvidence(200, stage)}. Missing {name}. Run ./parity.ps1 up; simulator must be on :3333.");
        var text = value.GetString();
        Ensure(!string.IsNullOrWhiteSpace(text), $"{SimulatorEvidence(200, stage)}. Empty {name}. {PreserveClientId}");
        return text!;
    }

    private static List<string> RequiredUrlList(JsonElement parent, string name, string stage)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            Fail($"{SimulatorEvidence(200, stage)}. Missing array {name}. Copy GET lists; do not authenticate.");
        return value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToList();
    }

    private static string? OptionalString(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any()) return directory.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    [GeneratedRegex("<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex HeadingRegex();
    [GeneratedRegex("<(?:span|a)[^>]*class=\"[^\"]*(?:error-message|govuk-error-message)[^\"]*\"[^>]*>(.*?)</(?:span|a)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex ValidationRegex();
    [GeneratedRegex("<a[^>]*(?:class=\"[^\"]*(?:govuk-header|govuk-service-navigation|govuk-link--inverse)[^\"]*\"|data-module=\"govuk-header\")[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex NavigationRegex();
    [GeneratedRegex("<dt[^>]*class=\"[^\"]*govuk-summary-list__key[^\"]*\"[^>]*>(?<key>.*?)</dt>\\s*<dd[^>]*class=\"[^\"]*govuk-summary-list__value[^\"]*\"[^>]*>(?<value>.*?)</dd>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex SummaryRowRegex();
    [GeneratedRegex("<a\\b(?=[^>]*\\bhref=\"(?<href>[^\"]+)\")[^>]*>\\s*(?:<[^>]+>\\s*)*Continue to GOV\\.UK One Login", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex ContinueHrefRegex();
    [GeneratedRegex("<[^>]+>")] private static partial Regex StripTags();
    [GeneratedRegex("\\s+")] private static partial Regex Whitespace();
    [GeneratedRegex("https?://[^\\s\"'<>]+", RegexOptions.IgnoreCase)] private static partial Regex AbsoluteUrlRegex();
    [GeneratedRegex("(?<![\\w:/])/[\\w./-]*\\?[^\\s\"'<>]+")] private static partial Regex RelativeQueryRegex();
    [GeneratedRegex("(?<name>Set-Cookie|Cookie|Authorization)[ \\t]*:[^\\n]*", RegexOptions.IgnoreCase)] private static partial Regex AuthHeaderRegex();
    [GeneratedRegex("\"(?<name>code|state|nonce|id_token|access_token|id_token_hint|session_state|client_secret|token|assertion|client_assertion)\"\\s*:\\s*\"[^\"]*\"|\\b(?<name>code|state|nonce|id_token|access_token|id_token_hint|session_state|client_secret|token|assertion|client_assertion)(?:=[^&\\s\"']+|\\s*:\\s*(?:\"[^\"]*\"|'[^']*'|[^\\s,;}\\]\"']+))", RegexOptions.IgnoreCase)] private static partial Regex SecretValueRegex();

    private sealed record SimulatorConfig(string ClientId, List<string> RedirectUrls, List<string> PostLogoutRedirectUrls, string? Email, string? Sub);
    private sealed record Capture(string App, AccountSemanticResult Semantics, bool ContainsRejectedEmail, bool NameFound, bool TrainingFound, bool ResearchFound);
    private sealed record AccountSemanticResult(int Status, string Path, string? Redirect, string? Heading, string? Name, string? TrainingEmailPreference, string? ResearchPreference, List<string> ValidationMessages, List<string> Navigation, string? ContentType);
    private sealed record ExpectedIdentity(string Email, string Sub, string Name);
    private sealed record AuthenticatedAccountParityReport(string Scenario, string Risk, ExpectedIdentity ExpectedIdentity, AccountSemanticResult Rails, AccountSemanticResult Dotnet, List<string> AcceptedNormalizations, List<string> Differences);
}
