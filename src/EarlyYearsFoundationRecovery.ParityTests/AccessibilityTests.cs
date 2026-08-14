using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

[Trait("Category", "Parity")]
public sealed class AccessibilityTests
{
    [ParityFact]
    public async Task Dotnet_critical_public_pages_have_no_serious_or_critical_axe_violations()
    {
        var (railsUrl, dotnetUrl) = ParityEnvironment.Require();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var railsViolations = await ScanAsync(browser, railsUrl);
        var dotnetViolations = await ScanAsync(browser, dotnetUrl);
        var newViolations = dotnetViolations.Except(railsViolations, StringComparer.Ordinal).ToList();
        Assert.True(newViolations.Count == 0, string.Join(Environment.NewLine, newViolations));
    }

    private static async Task<HashSet<string>> ScanAsync(IBrowser browser, string baseUrl)
    {
        var page = await browser.NewPageAsync();
        var failures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in new[] { "/", "/users/sign-in", "/about-training", "/settings/cookie-policy" })
        {
            await page.GotoAsync(baseUrl.TrimEnd('/') + path, new() { WaitUntil = WaitUntilState.NetworkIdle });
            var result = await page.RunAxe();
            failures.UnionWith(result.Violations
                .Where(x => x.Impact is "serious" or "critical")
                .Select(x => $"{path}: {x.Id} ({x.Impact})"));
        }
        await page.CloseAsync();
        return failures;
    }
}
