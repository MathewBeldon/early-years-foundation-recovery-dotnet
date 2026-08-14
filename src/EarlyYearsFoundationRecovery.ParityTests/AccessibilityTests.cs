using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.ParityTests;

public sealed class AccessibilityTests
{
    [Fact]
    public async Task Dotnet_critical_public_pages_have_no_serious_or_critical_axe_violations()
    {
        var railsUrl = Environment.GetEnvironmentVariable("RAILS_BASE_URL");
        var dotnetUrl = Environment.GetEnvironmentVariable("DOTNET_BASE_URL");
        if (string.IsNullOrWhiteSpace(railsUrl) || string.IsNullOrWhiteSpace(dotnetUrl)) return;

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
