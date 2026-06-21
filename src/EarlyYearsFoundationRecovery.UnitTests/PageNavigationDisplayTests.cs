using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class PageNavigationDisplayTests
{
    private static TrainingModuleContent CreateModule() =>
        new(
            Name: "module-1",
            Title: "Module 1",
            Description: "Description",
            Outcomes: "- outcome",
            Criteria: "- criteria",
            Duration: 1,
            Position: 1,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("what-to-expect", "interruption_page", "Before you begin", "Body"),
                new("key-concepts", "topic_intro", "Topic intro", "Body", [], null, null, Notes: true),
                new("applying-learning", "text_page", "Text page", "Body", [], null, null, Notes: true),
                TrainingPageContent.CreatePage("assessment-intro", "assessment_intro", "Assessment intro", "Body"),
                new("summative-q1", "summative", "Question 1", "Body", [], null, null),
                TrainingPageContent.CreatePage("certificate", "certificate", "Certificate", "Body"),
            ]);

    [Fact]
    public void BuildBackLinkText_UsesModulePosition()
    {
        var module = CreateModule();
        Assert.Equal("Back to Module 1 overview", PageNavigationDisplay.BuildBackLinkText(module));
    }

    [Fact]
    public void BuildPrevious_InterruptionPage_GoesToModuleOverview()
    {
        var module = CreateModule();
        var page = module.PageByName("what-to-expect")!;

        var (url, label) = PageNavigationDisplay.BuildPrevious(module, page);

        Assert.Equal("/modules/module-1", url);
        Assert.Equal("Previous", label);
    }

    [Fact]
    public void BuildNext_AssessmentIntro_ReturnsStartTest()
    {
        var module = CreateModule();
        var page = module.PageByName("assessment-intro")!;
        var next = module.NextPageAfter(page.Name)!;

        var (url, label) = PageNavigationDisplay.BuildNext(module, page, next);

        Assert.Equal("/modules/module-1/questionnaires/summative-q1", url);
        Assert.Equal("Start test", label);
    }

    [Fact]
    public void BuildNext_BeforeCertificate_ReturnsViewCertificate()
    {
        var module = CreateModule();
        var page = module.PageByName("summative-q1")!;
        var next = module.NextPageAfter(page.Name)!;

        var (url, label) = PageNavigationDisplay.BuildNext(module, page, next);

        Assert.Equal("/modules/module-1/content-pages/certificate", url);
        Assert.Equal("View certificate", label);
    }
}
