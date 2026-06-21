using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class ModuleProgressServiceTests
{
    private static TrainingModuleContent CreateModule() =>
        new(
            Name: "understanding-development",
            Title: "Understanding child development",
            Description: "Demo",
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 2,
            Position: 1,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("what-to-expect", "interruption_page", "Before you begin", string.Empty),
                TrainingPageContent.CreatePage("key-concepts", "topic_intro", "Key concepts", string.Empty),
                TrainingPageContent.CreatePage("applying-learning", "text_page", "Applying learning", string.Empty),
                new(
                    "check-understanding",
                    "formative",
                    "Check your understanding",
                    string.Empty,
                    [new QuestionAnswerOption("Correct", true)],
                    "Correct",
                    "Incorrect"),
                TrainingPageContent.CreatePage("assessment-intro", "assessment_intro", "Assessment intro", string.Empty),
                new(
                    "summative-q1",
                    "summative",
                    "Question 1",
                    string.Empty,
                    [new QuestionAnswerOption("Correct", true)],
                    "Correct",
                    "Incorrect"),
                TrainingPageContent.CreatePage("assessment-results", "assessment_results", "Results", string.Empty),
                TrainingPageContent.CreatePage("certificate", "certificate", "Module complete", string.Empty),
            ]);

    [Fact]
    public async Task RecordPageViewAsync_persists_each_visited_page()
    {
        var module = CreateModule();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        dbContext.Users.Add(new User { Email = "teacher@example.com" });
        await dbContext.SaveChangesAsync();

        var contentProvider = new StubTrainingContentProvider(module);
        var progressRepository = new UserModuleProgressRepository(dbContext);
        var service = new ModuleProgressService(progressRepository, contentProvider);

        var firstProgress = await service.RecordPageViewAsync(1, module.Name, "key-concepts");
        await service.RecordPageViewAsync(1, module.Name, "applying-learning");
        var latestProgress = await service.RecordPageViewAsync(1, module.Name, "check-understanding");

        var progress = await dbContext.UserModuleProgress.SingleAsync();
        Assert.Equal(progress.Id, firstProgress.Id);
        Assert.Equal(progress.Id, latestProgress.Id);
        Assert.Equal("check-understanding", latestProgress.LastPage);
        Assert.Equal(3, progress.VisitedPages.Count);
        Assert.True(progress.VisitedPages.ContainsKey("key-concepts"));
        Assert.True(progress.VisitedPages.ContainsKey("applying-learning"));
        Assert.True(progress.VisitedPages.ContainsKey("check-understanding"));
    }

    [Fact]
    public async Task CalculatePercentage_returns_100_for_completed_modules_with_legacy_visit_data()
    {
        var module = CreateModule();
        var progress = new UserModuleProgress
        {
            ModuleName = module.Name,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            VisitedPages = new Dictionary<string, bool> { ["certificate"] = true },
        };

        var percentage = ModuleProgressDisplay.CalculatePercentage(progress, module);

        Assert.Equal(100, percentage);
    }

    private sealed class StubTrainingContentProvider(TrainingModuleContent module) : ITrainingContentProvider
    {
        public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingModuleContent>>([module]);

        public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) =>
            GetLiveModulesAsync(cancellationToken);

        public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase) ? module : null);

        public Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetModuleByNameAsync(moduleName, cancellationToken).Result?.PageByName(pageName));
    }
}
