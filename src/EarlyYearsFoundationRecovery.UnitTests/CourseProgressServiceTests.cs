using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class CourseProgressServiceTests
{
    private static TrainingModuleContent LiveModule(string name, int position) =>
        new(
            Name: name,
            Title: name,
            Description: "Demo",
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 1,
            Position: position,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("intro", "topic_intro", "Intro", string.Empty),
                TrainingPageContent.CreatePage("certificate", "certificate", "Complete", string.Empty),
            ]);

    [Fact]
    public void Build_groups_modules_by_progress_state()
    {
        var modules = new[]
        {
            LiveModule("alpha", 1),
            LiveModule("bravo", 2),
            new(
                Name: "charlie",
                Title: "Charlie",
                Description: "Soon",
                Outcomes: string.Empty,
                Criteria: string.Empty,
                Duration: 1,
                Position: 3,
                Live: false,
                Pages: [TrainingPageContent.CreatePage("certificate", "certificate", "Complete", string.Empty)],
                Upcoming: "Coming soon"),
        };

        var progress = new Dictionary<string, UserModuleProgress>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = new()
            {
                ModuleName = "alpha",
                StartedAt = DateTime.UtcNow.AddDays(-1),
            },
            ["bravo"] = new()
            {
                ModuleName = "bravo",
                StartedAt = DateTime.UtcNow.AddDays(-2),
                CompletedAt = DateTime.UtcNow,
            },
        };

        var snapshot = CourseProgressService.Build(modules, progress, new Dictionary<string, Assessment>());

        Assert.Single(snapshot.InProgressModules);
        Assert.Equal("alpha", snapshot.InProgressModules[0].Module.Name);
        Assert.Empty(snapshot.AvailableModules);
        Assert.Single(snapshot.UpcomingModules);
        Assert.Equal("charlie", snapshot.UpcomingModules[0].Name);
        Assert.Single(snapshot.CompletedModules);
        Assert.Equal("bravo", snapshot.CompletedModules[0].Name);
        Assert.False(snapshot.CompletedAllModules);
        Assert.False(snapshot.ShowInProgressEmptyState);
        Assert.False(snapshot.ShowChooseAvailableModuleEmptyState);
    }
}
