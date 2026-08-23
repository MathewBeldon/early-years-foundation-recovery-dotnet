using System.Text.Json;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class DemoStaticPagesFixtureContractTests
{
    [Fact]
    public void Experts_page_matches_the_pinned_Rails_v1_5_0_copy()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryRoot(), "data", "demo-static-pages.json")));
        var page = document.RootElement.GetProperty("pages").EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == "experts");

        Assert.Equal("The experts", page.GetProperty("title").GetString());
        Assert.Equal("The experts", page.GetProperty("heading").GetString());
        Assert.False(page.GetProperty("footer").GetBoolean());

        var body = page.GetProperty("body").GetString();
        Assert.NotNull(body);
        Assert.Contains("working as early years practitioners", body);
        Assert.Contains("Foundation Degree in Early Childhood Studies", body);
        Assert.Contains("D32/D33 Assessor Qualification", body);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
                return directory.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
