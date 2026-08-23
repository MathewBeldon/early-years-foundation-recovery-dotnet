using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Contentful.Core.Models;
using Newtonsoft.Json.Linq;
using ContentfulFile = Contentful.Core.Models.File;

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

    [Theory]
    [InlineData("//images.ctfassets.net/space/image/module.jpg", "https://images.ctfassets.net/space/image/module.jpg")]
    [InlineData("https://images.ctfassets.net/space/image/module.jpg?fit=fill", "https://images.ctfassets.net/space/image/module.jpg?fit=fill")]
    public void Contentful_thumbnail_is_mapped_and_normalized(string source, string expected)
    {
        var fields = ValidModule();
        fields.Image = Image(source);

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.Equal(expected, mapped.ThumbnailUrl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("http://images.ctfassets.net/space/image/module.jpg")]
    [InlineData("https://evil.example/module.jpg")]
    [InlineData("https://images.ctfassets.net.evil.example/module.jpg")]
    [InlineData("https://images.ctfassets.net:444/module.jpg")]
    [InlineData("https://user@images.ctfassets.net/module.jpg")]
    public void Missing_or_untrusted_thumbnail_is_not_mapped(string? source)
    {
        var fields = ValidModule();
        fields.Image = source is null ? null : Image(source);

        var mapped = ContentfulContentMapper.ToModule(fields);

        Assert.Null(mapped.ThumbnailUrl);
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
        Image = Image("//images.ctfassets.net/space/image/module.jpg"),
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

    private static Asset Image(string url) => new()
    {
        Title = "Module image",
        Description = "Decorative module image",
        File = new ContentfulFile
        {
            Url = url,
            FileName = "module.jpg",
            ContentType = "image/jpeg",
            Details = new FileDetails(),
        },
    };
}
