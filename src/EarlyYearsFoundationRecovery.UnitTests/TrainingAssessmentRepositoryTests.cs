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

    [Fact]
    public async Task Response_lookups_exclude_responses_with_a_different_question_type()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Responses.AddRange(
            CreateResponse("formative", assessmentId: null),
            CreateResponse("formative", assessmentId: 1),
            CreateResponse("summative", assessmentId: 1));
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);

        var response = await repository.GetResponseAsync(1, "module-one", "question-one", "formative");
        var assessmentResponse = await repository.GetResponseForAssessmentAsync(
            1,
            "module-one",
            "question-one",
            1,
            "summative");

        Assert.NotNull(response);
        Assert.Equal("formative", response.QuestionType);
        Assert.NotNull(assessmentResponse);
        Assert.Equal("summative", assessmentResponse.QuestionType);
    }

    [Fact]
    public async Task GetResponseForAssessmentAsync_returns_newest_summative_response_without_deleting_duplicates()
    {
        await using var dbContext = CreateDbContext();
        var older = CreateResponse("summative", assessmentId: 7);
        older.CreatedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var newer = CreateResponse("summative", assessmentId: 7);
        newer.CreatedAt = older.CreatedAt.AddMinutes(1);
        dbContext.Responses.AddRange(older, newer);
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);

        var response = await repository.GetResponseForAssessmentAsync(
            1,
            "module-one",
            "question-one",
            7,
            "summative");

        Assert.NotNull(response);
        Assert.Equal(newer.Id, response.Id);
        Assert.Equal(2, await dbContext.Responses.CountAsync());
    }

    [Fact]
    public async Task GetResponseForAssessmentAsync_uses_id_as_tie_breaker_for_equal_created_at()
    {
        await using var dbContext = CreateDbContext();
        var timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var first = CreateResponse("summative", assessmentId: 7);
        first.CreatedAt = timestamp;
        var second = CreateResponse("summative", assessmentId: 7);
        second.CreatedAt = timestamp;
        dbContext.Responses.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);

        var response = await repository.GetResponseForAssessmentAsync(
            1,
            "module-one",
            "question-one",
            7,
            "summative");

        Assert.NotNull(response);
        Assert.Equal(Math.Max(first.Id, second.Id), response.Id);
        Assert.Equal(2, await dbContext.Responses.CountAsync());
    }

    [Fact]
    public async Task GetResponseForAssessmentAsync_keeps_responses_for_separate_assessments_separate()
    {
        await using var dbContext = CreateDbContext();
        var first = CreateResponse("summative", assessmentId: 7);
        first.Answers = ["first"];
        var second = CreateResponse("summative", assessmentId: 8);
        second.Answers = ["second"];
        dbContext.Responses.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);

        var firstResponse = await repository.GetResponseForAssessmentAsync(
            1,
            "module-one",
            "question-one",
            7,
            "summative");
        var secondResponse = await repository.GetResponseForAssessmentAsync(
            1,
            "module-one",
            "question-one",
            8,
            "summative");

        Assert.Equal("first", Assert.Single(firstResponse!.Answers));
        Assert.Equal("second", Assert.Single(secondResponse!.Answers));
        Assert.Equal(2, await dbContext.Responses.CountAsync());
    }

    private static Response CreateResponse(string questionType, long? assessmentId) =>
        new()
        {
            UserId = 1,
            TrainingModule = "module-one",
            QuestionName = "question-one",
            QuestionType = questionType,
            AssessmentId = assessmentId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
}
