using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class VisitedPagesBackfillTests
{
    [Fact]
    public async Task RunAsync_merges_missing_content_pages_for_completed_modules()
    {
        var module = new TrainingModuleContent(
            Name: "understanding-development",
            Title: "Understanding child development",
            Description: string.Empty,
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 2,
            Position: 1,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("what-to-expect", "interruption_page", "Before you begin", string.Empty),
                TrainingPageContent.CreatePage("key-concepts", "topic_intro", "Key concepts", string.Empty),
                TrainingPageContent.CreatePage("certificate", "certificate", "Module complete", string.Empty),
            ]);

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        dbContext.UserModuleProgress.Add(new UserModuleProgress
        {
            UserId = 1,
            ModuleName = module.Name,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            VisitedPages = new Dictionary<string, bool> { ["key-concepts"] = true },
        });
        await dbContext.SaveChangesAsync();

        await VisitedPagesBackfill.RunAsync(
            dbContext,
            new StubTrainingContentProvider(module),
            NullLogger.Instance);

        var progress = await dbContext.UserModuleProgress.SingleAsync();
        Assert.True(progress.VisitedPages.ContainsKey("key-concepts"));
        Assert.True(progress.VisitedPages.ContainsKey("certificate"));
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
