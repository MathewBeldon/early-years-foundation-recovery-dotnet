using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class TrainingAssessmentRepositoryTests
{
    private static ApplicationDbContext CreateDbContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetLatestAssessmentsByModuleAsync_returns_latest_assessment_for_each_requested_module()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Assessments.AddRange(
            new Assessment
            {
                UserId = 1,
                TrainingModule = "module-one",
                StartedAt = DateTime.UtcNow.AddHours(-2),
                Score = 10,
            },
            new Assessment
            {
                UserId = 1,
                TrainingModule = "module-one",
                StartedAt = DateTime.UtcNow.AddHours(-1),
                Score = 90,
            },
            new Assessment
            {
                UserId = 1,
                TrainingModule = "module-two",
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                Score = 75,
            },
            new Assessment
            {
                UserId = 1,
                TrainingModule = "module-three",
                StartedAt = DateTime.UtcNow,
                Score = 100,
            },
            new Assessment
            {
                UserId = 2,
                TrainingModule = "module-one",
                StartedAt = DateTime.UtcNow,
                Score = 50,
            });
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);

        var assessments = await repository.GetLatestAssessmentsByModuleAsync(1, ["module-one", "module-two"]);

        Assert.Equal(2, assessments.Count);
        Assert.Equal(90, assessments["module-one"].Score);
        Assert.Equal(75, assessments["module-two"].Score);
        Assert.DoesNotContain("module-three", assessments.Keys);
    }

    [Fact]
    public async Task SaveAssessmentAsync_updates_detached_existing_assessment()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using (var seedContext = CreateDbContext(databaseName))
        {
            seedContext.Assessments.Add(new Assessment
            {
                UserId = 1,
                TrainingModule = "module-one",
                StartedAt = DateTime.UtcNow,
            });
            await seedContext.SaveChangesAsync();
        }

        Assessment detached;
        await using (var readContext = CreateDbContext(databaseName))
        {
            detached = await readContext.Assessments.AsNoTracking().SingleAsync();
        }

        detached.Score = 80;
        detached.Passed = true;
        detached.CompletedAt = DateTime.UtcNow;

        await using var writeContext = CreateDbContext(databaseName);
        var repository = new TrainingAssessmentRepository(writeContext);
        await repository.SaveAssessmentAsync(detached);

        var saved = await writeContext.Assessments.SingleAsync();
        Assert.Equal(80, saved.Score);
        Assert.True(saved.Passed);
        Assert.NotNull(saved.CompletedAt);
    }
}
