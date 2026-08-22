using System.Text.Json;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class DemoTrainingFixtureContractTests
{
    [Fact]
    public void Module_one_has_a_submodule_intro_before_the_first_topic()
    {
        using var document = LoadFixture();
        var pages = document.RootElement.GetProperty("modules").EnumerateArray()
            .Single(module => module.GetProperty("name").GetString() == "module-1")
            .GetProperty("pages").EnumerateArray().ToArray();

        var names = pages.Select(page => page.GetProperty("name").GetString()).ToArray();
        var introIndex = Array.IndexOf(names, "module-1-introduction");
        var topicIndex = Array.IndexOf(names, "key-concepts");
        Assert.True(introIndex > 0 && introIndex < topicIndex);
        Assert.Equal("sub_module_intro", pages[introIndex].GetProperty("pageType").GetString());
    }

    [Fact]
    public void Module_four_has_the_feedback_thankyou_certificate_boundary_in_order()
    {
        using var document = LoadFixture();
        var pages = ModuleFourPages(document);

        var boundary = pages
            .Where(page => page.GetProperty("name").GetString() is "feedback-q1" or "thankyou" or "certificate")
            .Select(page => page.GetProperty("name").GetString() ?? string.Empty)
            .ToArray();

        Assert.Equal(["feedback-q1", "thankyou", "certificate"], boundary);

        var feedback = pages.Single(page => page.GetProperty("name").GetString() == "feedback-q1");
        Assert.Equal("feedback", feedback.GetProperty("pageType").GetString());
        Assert.Equal(3, feedback.GetProperty("answers").GetArrayLength());
        Assert.True(feedback.GetProperty("skippable").GetBoolean());
        Assert.All(feedback.GetProperty("answers").EnumerateArray(), answer => Assert.False(answer.GetProperty("correct").GetBoolean()));
        Assert.Equal("thankyou", pages.Single(page => page.GetProperty("name").GetString() == "thankyou").GetProperty("pageType").GetString());
        Assert.Equal("certificate", pages.Single(page => page.GetProperty("name").GetString() == "certificate").GetProperty("pageType").GetString());
    }

    [Fact]
    public void Synthetic_module_four_is_explicitly_not_a_release_valid_contentful_module()
    {
        using var document = LoadFixture();
        var pageTypes = ModuleFourPages(document)
            .Select(page => page.GetProperty("pageType").GetString())
            .ToArray();

        Assert.DoesNotContain("confidence_outro", pageTypes);

        // The demo fixture is intentionally a small navigation/grading slice. It does not
        // claim to satisfy the full Contentful ContentIntegrity contract used at cutover.
        Assert.True(pageTypes.Count(type => type == "summative") < 10);
        Assert.DoesNotContain("text_page", pageTypes);
        Assert.DoesNotContain("video_page", pageTypes);
        Assert.DoesNotContain("confidence", pageTypes);
        Assert.DoesNotContain("recap_page", pageTypes);
        Assert.DoesNotContain("summary_intro", pageTypes);
        Assert.DoesNotContain("confidence_intro", pageTypes);
    }

    private static JsonDocument LoadFixture() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "data", "demo-training-content.json")));

    private static JsonElement[] ModuleFourPages(JsonDocument document) => document.RootElement
        .GetProperty("modules")
        .EnumerateArray()
        .Single(module => module.GetProperty("name").GetString() == "module-4")
        .GetProperty("pages")
        .EnumerateArray()
        .ToArray();

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
