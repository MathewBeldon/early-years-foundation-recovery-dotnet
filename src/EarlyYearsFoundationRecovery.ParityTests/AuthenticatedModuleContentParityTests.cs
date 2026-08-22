using System.Text.Json;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>Rails v1.5.0 ac546721 authenticated normal module-content contract.</summary>
public sealed partial class AuthenticatedAccountParityTests
{
    private const string ModuleContentEmail = "module-content@example.test";
    private const string ModuleContentSub = "synthetic-module-content";

    [ParityFact]
    public async Task Rails_and_dotnet_have_the_same_normal_module_content_journey()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();
        using var playwright = await Playwright.CreateAsync();
        await using var simulator = await playwright.APIRequest.NewContextAsync(new() { BaseURL = SimulatorBaseUrl });
        await using var rails = await playwright.APIRequest.NewContextAsync(new() { BaseURL = railsUrl });
        await using var dotnet = await playwright.APIRequest.NewContextAsync(new() { BaseURL = dotnetUrl });

        await ConfigureSimulatorAsync(simulator, ModuleContentEmail, ModuleContentSub);
        await SignInRailsAsync(rails);
        await FollowAuthHopsAsync(dotnet, "/users/auth/openid_connect", ".NET");

        var railsResult = await ExerciseModuleContentAsync(rails, "Rails");
        var dotnetResult = await ExerciseModuleContentAsync(dotnet, ".NET");
        var differences = CompareModuleContent(railsResult, dotnetResult);
        var reportDirectory = Path.Combine(FindRepositoryRoot(), "TestResults");
        Directory.CreateDirectory(reportDirectory);
        await File.WriteAllTextAsync(Path.Combine(reportDirectory, "authenticated-module-content-parity.json"),
            JsonSerializer.Serialize(new
            {
                scenario = "authenticated-normal-module-content",
                risk = "P1",
                expectedIdentity = new { email = ModuleContentEmail, sub = ModuleContentSub },
                rails = railsResult,
                dotnet = dotnetResult,
                acceptedNormalizations = new[]
                {
                    "framework HTML and cookies",
                    "generated IDs and timestamps",
                    "the synthetic module is unreleased to the Rails overview controller, which records module_overview_page then redirects to /my-modules; .NET's demo provider renders the overview",
                },
                differences,
            }, new JsonSerializerOptions { WriteIndented = true }));
        Ensure(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    private static async Task<ModuleContentResult> ExerciseModuleContentAsync(IAPIRequestContext context, string app)
    {
        var pages = new List<ModuleContentPage> { await CaptureModulePageAsync(context, "/modules/module-1", app) };
        var index = await FetchAsync(context, "/modules/module-1/content-pages", app);
        var redirect = new ModuleContentRedirect(index.Status, SanitizePath(LocationOf(index) ?? string.Empty));
        foreach (var path in new[]
        {
            "/modules/module-1/content-pages/what-to-expect",
            "/modules/module-1/content-pages/module-1-introduction",
            "/modules/module-1/content-pages/key-concepts",
            "/modules/module-1/content-pages/applying-learning",
            "/modules/module-1/content-pages/module-1-introduction",
            "/modules/module-1/content-pages/key-concepts",
        })
            pages.Add(await CaptureModulePageAsync(context, path, app));
        return new(app, redirect, pages);
    }

    private static async Task<ModuleContentPage> CaptureModulePageAsync(IAPIRequestContext context, string path, string app)
    {
        var response = await FetchAsync(context, path, app);
        return new(response.Status, SanitizePath(path), Extract(HeadingRegex(), await response.TextAsync()));
    }

    private static List<string> CompareModuleContent(ModuleContentResult rails, ModuleContentResult dotnet)
    {
        var differences = new List<string>();
        const string expectedRedirect = "/modules/module-1/content-pages/what-to-expect";
        foreach (var result in new[] { rails, dotnet })
        {
            if (result.Index.Status is not >= 300 or >= 400 || result.Index.Location != expectedRedirect)
                differences.Add($"{result.App} content index returned {result.Index.Status} '{result.Index.Location}'; expected {expectedRedirect}.");
            foreach (var page in result.Pages.Skip(1))
                if (page.Status != 200 || string.IsNullOrWhiteSpace(page.Heading))
                    differences.Add($"{result.App} {page.Path} returned {page.Status} heading '{page.Heading}'.");
        }
        var railsOverview = rails.Pages[0];
        var dotnetOverview = dotnet.Pages[0];
        if (railsOverview.Status != 302 || dotnetOverview.Status != 200 || dotnetOverview.Heading != "Module 1")
            differences.Add($"Synthetic overview normalization changed: Rails={railsOverview.Status}, .NET={dotnetOverview.Status} '{dotnetOverview.Heading}'.");
        if (!rails.Pages.Skip(1).Select(x => (x.Path, x.Heading)).SequenceEqual(dotnet.Pages.Skip(1).Select(x => (x.Path, x.Heading))))
            differences.Add("Rails and .NET normal module-content paths/headings differ.");
        return differences;
    }

    private sealed record ModuleContentResult(string App, ModuleContentRedirect Index, IReadOnlyList<ModuleContentPage> Pages);
    private sealed record ModuleContentRedirect(int Status, string Location);
    private sealed record ModuleContentPage(int Status, string Path, string? Heading);
}
