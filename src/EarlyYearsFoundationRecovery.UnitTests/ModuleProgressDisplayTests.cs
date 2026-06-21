using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class ModuleProgressDisplayTests
{
    [Fact]
    public void BuildProgressSummary_shows_assessment_score_when_failed()
    {
        var assessment = new Assessment { Score = 50, Passed = false, CompletedAt = DateTime.UtcNow };

        var summary = ModuleProgressDisplay.BuildProgressSummary(100, assessment);

        Assert.Contains("100% of pages viewed", summary);
        Assert.Contains("50%", summary);
        Assert.Contains("pass mark is 70%", summary);
    }

    [Fact]
    public void BuildProgressSummary_shows_complete_when_passed()
    {
        var assessment = new Assessment { Score = 100, Passed = true, CompletedAt = DateTime.UtcNow };

        var summary = ModuleProgressDisplay.BuildProgressSummary(100, assessment);

        Assert.Equal("100% complete", summary);
    }

    [Fact]
    public void BuildRetakeOrResultsLink_returns_retake_when_failed()
    {
        var module = new TrainingModuleContent(
            "demo",
            "Demo",
            "Description",
            string.Empty,
            string.Empty,
            1,
            1,
            true,
            [
                TrainingPageContent.CreatePage("assessment-intro", "assessment_intro", "Intro", string.Empty),
                TrainingPageContent.CreatePage("assessment-results", "assessment_results", "Results", string.Empty),
            ]);

        var assessment = new Assessment { Score = 50, Passed = false, CompletedAt = DateTime.UtcNow };

        var (label, url) = ModuleProgressDisplay.BuildRetakeOrResultsLink(module, assessment);

        Assert.Equal("Retake end of module test", label);
        Assert.Equal("/modules/demo/content-pages/assessment-intro", url);
    }
}
