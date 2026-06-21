using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class AssessmentRetakeTests
{
    private static TrainingModuleContent CreateDemoModule() =>
        new(
            Name: "demo-module",
            Title: "Demo module",
            Description: "Demo",
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 1,
            Position: 1,
            Live: true,
            Pages:
            [
                TrainingPageContent.CreatePage("intro", "topic_intro", "Intro", string.Empty),
                new(
                    "summative-1",
                    "summative",
                    "Question 1",
                    string.Empty,
                    [new QuestionAnswerOption("Correct", true), new QuestionAnswerOption("Wrong", false)],
                    "Correct",
                    "Wrong"),
                new(
                    "summative-2",
                    "summative",
                    "Question 2",
                    string.Empty,
                    [new QuestionAnswerOption("Correct", true), new QuestionAnswerOption("Wrong", false)],
                    "Correct",
                    "Wrong"),
                TrainingPageContent.CreatePage("assessment-results", "assessment_results", "Results", string.Empty),
            ]);

    [Fact]
    public async Task ResolveSummativeAssessmentAsync_creates_new_assessment_after_failed_attempt()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        dbContext.Users.Add(new User { Email = "teacher@example.com" });
        await dbContext.SaveChangesAsync();

        var failedAssessment = new Assessment
        {
            UserId = 1,
            TrainingModule = "demo-module",
            Score = 50,
            Passed = false,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-30),
        };
        dbContext.Assessments.Add(failedAssessment);
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);
        var service = new AssessmentProgressService(repository);

        var assessment = await service.ResolveSummativeAssessmentAsync(1, "demo-module");

        Assert.NotEqual(failedAssessment.Id, assessment.Id);
        Assert.Null(assessment.CompletedAt);
    }

    [Fact]
    public async Task SubmitAnswerAsync_creates_new_summative_response_on_retake()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new ApplicationDbContext(options);
        dbContext.Users.Add(new User { Email = "teacher@example.com" });
        await dbContext.SaveChangesAsync();

        var failedAssessment = new Assessment
        {
            UserId = 1,
            TrainingModule = "demo-module",
            Score = 0,
            Passed = false,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-30),
        };
        dbContext.Assessments.Add(failedAssessment);
        dbContext.Responses.Add(new Response
        {
            UserId = 1,
            TrainingModule = "demo-module",
            QuestionName = "summative-1",
            QuestionType = "summative",
            AssessmentId = failedAssessment.Id,
            Answers = ["Wrong"],
            Correct = false,
        });
        await dbContext.SaveChangesAsync();

        var repository = new TrainingAssessmentRepository(dbContext);
        var assessmentProgressService = new AssessmentProgressService(repository);
        var questionAnswerService = new QuestionAnswerService(repository, assessmentProgressService);
        var module = CreateDemoModule();
        var question = module.PageByName("summative-1")!;

        await questionAnswerService.SubmitAnswerAsync(1, module, question, "Correct");

        var assessments = await dbContext.Assessments.OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(2, assessments.Count);

        var latestAssessment = assessments[^1];
        Assert.Null(latestAssessment.CompletedAt);

        var responses = await dbContext.Responses
            .Where(r => r.QuestionName == "summative-1")
            .OrderBy(r => r.Id)
            .ToListAsync();
        Assert.Equal(2, responses.Count);
        Assert.Equal(failedAssessment.Id, responses[0].AssessmentId);
        Assert.Equal(latestAssessment.Id, responses[1].AssessmentId);
        Assert.Equal("Correct", responses[1].Answers.Single());
    }
}
