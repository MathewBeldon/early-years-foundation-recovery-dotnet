using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Newtonsoft.Json.Linq;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ContentfulContentIntegrityTests
{
    [Fact]
    public void Valid_module_is_classified_live_without_a_fields_live_property()
    {
        var fields = ValidModule();

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.True(mapped.Live);
        Assert.DoesNotContain(
            typeof(TrainingModuleFields).GetProperties(),
            property => string.Equals(property.Name, "Live", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Missing_required_module_content_is_classified_not_live()
    {
        var fields = ValidModule();
        fields.Description = null;

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.False(mapped.Live);
    }

    [Fact]
    public void Malformed_or_incomplete_module_tree_is_classified_not_live_without_throwing()
    {
        var fields = ValidModule();
        fields.Pages =
        [
            new PageFields { PageType = "interruption_page" },
            new PageFields { PageType = "not-a-real-page-type" },
        ];

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.False(mapped.Live);
    }

    [Fact]
    public void Null_pages_are_classified_not_live_at_the_provider_boundary()
    {
        var fields = ValidModule();
        fields.Pages = null;

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.False(mapped.Live);
        Assert.Empty(mapped.Pages);
    }

    [Fact]
    public void Legacy_object_answer_shape_remains_live_compatible()
    {
        var fields = ValidModule();
        foreach (var page in fields.Pages!.Where(page => page.PageType is "formative" or "summative"))
        {
            page.Answers = ObjectAnswers();
        }

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.True(mapped.Live);
    }

    private static TrainingModuleFields ValidModule() => new()
    {
        Name = "module-1",
        Title = "Module one",
        Description = "Description",
        About = "About",
        Outcomes = "Outcomes",
        Criteria = "Criteria",
        Duration = 30,
        Position = 1,
        Image = new JObject([new JProperty("sys", new JObject())]),
        Upcoming = "Coming soon",
        Pages =
        [
            Page("interruption_page"),
            Page("sub_module_intro"),
            Page("topic_intro"),
            Page("text_page"),
            Page("video_page"),
            Page("formative", Answers()),
            Page("feedback"),
            Page("assessment_intro"),
            Page("confidence"),
            Page("confidence"),
            Page("confidence"),
            Page("confidence"),
            Page("recap_page"),
            ..Enumerable.Range(1, 10).Select(_ => Page("summative", Answers())),
            Page("summary_intro"),
            Page("confidence_intro"),
            Page("assessment_results"),
            Page("thankyou"),
            Page("certificate"),
        ],
    };

    private static PageFields Page(string pageType, object? answers = null) => new()
    {
        Name = pageType,
        PageType = pageType,
        Heading = pageType,
        Body = pageType,
        Answers = answers,
    };

    private static JArray Answers() => JArray.Parse("[[\"Wrong\",false],[\"Right\",true]]");

    private static JArray ObjectAnswers() => JArray.Parse("[{\"text\":\"Wrong\"},{\"text\":\"Right\",\"correct\":true}]");
}
