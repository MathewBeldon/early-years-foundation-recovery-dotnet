using System.Text.Json;
using System.Text.RegularExpressions;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ProtocolFakeVideoContractTests
{
    [Fact]
    public void Protocol_fake_models_demo_video_as_a_distinct_contentful_video_entry()
    {
        var root = RepositoryRoot();
        var fake = File.ReadAllText(Path.Combine(root, "parity", "fakes", "protocol_fake.py"));
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "data", "demo-training-content.json")));
        var video = fixture.RootElement.GetProperty("modules").EnumerateArray()
            .Single(module => module.GetProperty("name").GetString() == "module-1")
            .GetProperty("pages").EnumerateArray()
            .Single(page => page.GetProperty("name").GetString() == "expert-video");

        Assert.Equal("video_page", video.GetProperty("pageType").GetString());
        Assert.Contains("\"video\":", fake, StringComparison.Ordinal);
        Assert.Contains("elif page.get(\"pageType\") == \"video_page\"", fake, StringComparison.Ordinal);
        Assert.Contains("child = video_entry(page, identifier)", fake, StringComparison.Ordinal);
        Assert.Contains("return entry(identifier, \"video\"", fake, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"page_entry\([^\r\n]*video_page", RegexOptions.IgnoreCase), fake);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (directory.EnumerateFiles("*.slnx").Any()) return directory.FullName;
        throw new InvalidOperationException("Could not locate repository root.");
    }
}
