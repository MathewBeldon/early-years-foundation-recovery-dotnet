using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class SectionBarBuilderTests
{
    [Fact]
    public void Build_returns_page_position_within_section()
    {
        var module = new TrainingModuleContent(
            Name: "module-1",
            Title: "Module 1",
            Description: "Description",
            Outcomes: "",
            Criteria: "",
            Duration: 1,
            Position: 1,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("intro", "topic_intro", "Topic intro", "Body"),
                TrainingPageContent.CreatePage("text", "text_page", "Text page", "Body"),
                TrainingPageContent.CreatePage("done", "certificate", "Certificate", "Body"),
            ]);

        var sectionBar = SectionBarBuilder.Build(module, module.PageByName("intro")!);

        Assert.NotNull(sectionBar);
        Assert.Equal("Page 1 of 2", sectionBar.PageNumbers);
        Assert.Equal(50, sectionBar.Percentage);
    }
}
