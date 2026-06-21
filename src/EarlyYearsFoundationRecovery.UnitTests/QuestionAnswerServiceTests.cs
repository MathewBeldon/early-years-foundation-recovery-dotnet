using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class QuestionAnswerServiceTests
{
    private const string CorrectAnswer = "correct";
    private const string WrongAnswer = "wrong";

    private static ApplicationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static QuestionAnswerService CreateService(ApplicationDbContext dbContext)
    {
        var repository = new TrainingAssessmentRepository(dbContext);
        return new QuestionAnswerService(repository, new AssessmentProgressService(repository));
    }

    private static TrainingPageContent SummativeQuestion(string name) =>
        new(name, "summative", name, string.Empty,
            [new QuestionAnswerOption(CorrectAnswer, true), new QuestionAnswerOption(WrongAnswer, false)],
            "Well done", "Try again");

    private static TrainingModuleContent CreateModule(int summativeCount)
    {
        var pages = new List<TrainingPageContent>
        {
            TrainingPageContent.CreatePage("intro", "topic_intro", "Intro", string.Empty),
            new("formative-q", "formative", "Formative", string.Empty,
                [new QuestionAnswerOption(CorrectAnswer, true), new QuestionAnswerOption(WrongAnswer, false)],
                "Nice", "Nope"),
        };

        for (var i = 1; i <= summativeCount; i++)
        {
            pages.Add(SummativeQuestion($"summative-q{i}"));
        }

        pages.Add(TrainingPageContent.CreatePage("assessment-results", "assessment_results", "Results", string.Empty));

        return new TrainingModuleContent(
            Name: "module-one",
            Title: "Module one",
            Description: "Demo",
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 1,
            Position: 1,
            Live: true,
            Pages: pages);
    }

    // Submits the given answers in order; returns the result of the final submission (which grades the assessment).
    private static async Task<QuestionAnswerResult> SubmitSummativeAnswersAsync(
        QuestionAnswerService service,
        TrainingModuleContent module,
        long userId,
        IReadOnlyList<(string QuestionName, bool Correct)> answers)
    {
        QuestionAnswerResult? last = null;
        foreach (var (questionName, correct) in answers)
        {
            var question = module.PageByName(questionName)!;
            last = await service.SubmitAnswerAsync(
                userId,
                module,
                question,
                correct ? CorrectAnswer : WrongAnswer);
        }

        return last!;
    }

    [Fact]
    public async Task GradeAssessment_all_correct_scores_100_and_passes()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 4);
        var service = CreateService(dbContext);

        var answers = Enumerable.Range(1, 4).Select(i => ($"summative-q{i}", true)).ToList();
        var result = await SubmitSummativeAnswersAsync(service, module, userId: 1, answers);

        Assert.NotNull(result.GradedAssessment);
        Assert.Equal(100f, result.GradedAssessment!.Score);
        Assert.True(result.GradedAssessment.Passed);
        Assert.NotNull(result.GradedAssessment.CompletedAt);
    }

    [Fact]
    public async Task GradeAssessment_at_exactly_pass_threshold_passes()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 10);
        var service = CreateService(dbContext);

        // 7 of 10 correct == 70% == PassThreshold.
        var answers = Enumerable.Range(1, 10)
            .Select(i => ($"summative-q{i}", i <= 7))
            .ToList();
        var result = await SubmitSummativeAnswersAsync(service, module, userId: 1, answers);

        Assert.NotNull(result.GradedAssessment);
        Assert.Equal(QuestionAnswerService.PassThreshold, result.GradedAssessment!.Score);
        Assert.True(result.GradedAssessment.Passed);
    }

    [Fact]
    public async Task GradeAssessment_below_threshold_fails_but_is_still_graded()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 10);
        var service = CreateService(dbContext);

        // 6 of 10 correct == 60% < PassThreshold.
        var answers = Enumerable.Range(1, 10)
            .Select(i => ($"summative-q{i}", i <= 6))
            .ToList();
        var result = await SubmitSummativeAnswersAsync(service, module, userId: 1, answers);

        Assert.NotNull(result.GradedAssessment);
        Assert.Equal(60f, result.GradedAssessment!.Score);
        Assert.False(result.GradedAssessment.Passed);
        Assert.NotNull(result.GradedAssessment.CompletedAt);
    }

    [Fact]
    public async Task GradeAssessment_counts_unanswered_questions_as_incorrect()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 4);
        var service = CreateService(dbContext);

        // Skip q3 entirely; the last question (q4) still triggers grading. The score divides by the
        // module's summative question count, so the skipped question lowers the score to 75%.
        var answers = new List<(string, bool)>
        {
            ("summative-q1", true),
            ("summative-q2", true),
            ("summative-q4", true),
        };
        var result = await SubmitSummativeAnswersAsync(service, module, userId: 1, answers);

        Assert.NotNull(result.GradedAssessment);
        Assert.Equal(75f, result.GradedAssessment!.Score);
        Assert.True(result.GradedAssessment.Passed);
    }

    [Fact]
    public async Task SubmitAnswer_for_formative_question_is_idempotent()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 1);
        var question = module.PageByName("formative-q")!;
        var service = CreateService(dbContext);

        var first = await service.SubmitAnswerAsync(1, module, question, CorrectAnswer);
        // A repeated submission (even a different answer) returns the originally stored response.
        var second = await service.SubmitAnswerAsync(1, module, question, WrongAnswer);

        Assert.True(first.IsCorrect);
        Assert.True(second.IsCorrect);

        var responses = await dbContext.Responses
            .Where(r => r.QuestionName == "formative-q")
            .ToListAsync();
        Assert.Single(responses);
    }

    [Fact]
    public async Task SubmitAnswer_with_unknown_option_is_invalid()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 1);
        var question = module.PageByName("summative-q1")!;
        var service = CreateService(dbContext);

        var result = await service.SubmitAnswerAsync(1, module, question, "not-an-option");

        Assert.False(result.IsValid);
        Assert.Null(result.GradedAssessment);
        Assert.Empty(await dbContext.Responses.ToListAsync());
    }
}
