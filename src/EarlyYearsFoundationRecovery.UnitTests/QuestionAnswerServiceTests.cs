using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;
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

    private static TrainingModuleContent CreateModule(int summativeCount, string moduleName = "module-one")
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
            Name: moduleName,
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
    public async Task GradeAssessment_uses_only_summative_responses_and_preserves_raw_module_four_percentage()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 3, moduleName: "module-4");
        var assessment = new Assessment
        {
            UserId = 1,
            TrainingModule = module.Name,
            StartedAt = DateTime.UtcNow.AddMinutes(-10),
        };
        dbContext.Assessments.Add(assessment);
        await dbContext.SaveChangesAsync();

        dbContext.Responses.Add(new Response
        {
            UserId = 1,
            TrainingModule = module.Name,
            QuestionName = "formative-q",
            QuestionType = "formative",
            AssessmentId = assessment.Id,
            Correct = true,
        });
        await dbContext.SaveChangesAsync();

        var result = await SubmitSummativeAnswersAsync(
            CreateService(dbContext),
            module,
            userId: 1,
            [
                ("summative-q1", true),
                ("summative-q2", true),
                ("summative-q3", false),
            ]);

        Assert.NotNull(result.GradedAssessment);
        Assert.Equal(200f / 3f, result.GradedAssessment!.Score);
        Assert.False(result.GradedAssessment.Passed);
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

    [Fact]
    public async Task SubmitAnswer_reuses_existing_passed_assessment_and_persists_one_based_answer_id()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 2);
        var passed = new Assessment
        {
            UserId = 42,
            TrainingModule = module.Name,
            Score = 75,
            Passed = true,
            StartedAt = DateTime.UtcNow.AddDays(-1),
            CompletedAt = DateTime.UtcNow.AddDays(-1).AddMinutes(30),
        };
        dbContext.Assessments.Add(passed);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        var result = await service.SubmitAnswerAsync(42, module, module.PageByName("summative-q1")!, "1");

        Assert.True(result.IsValid);
        Assert.Equal(1, result.AnswerId);
        var assessment = Assert.Single(await dbContext.Assessments.ToListAsync());
        Assert.Equal(passed.Id, assessment.Id);
        Assert.Equal(75, assessment.Score);
        Assert.True(assessment.Passed);
        var response = Assert.Single(await dbContext.Responses.ToListAsync());
        Assert.Equal(passed.Id, response.AssessmentId);
        Assert.Equal("1", Assert.Single(response.Answers));
    }

    [Fact]
    public async Task SubmitAnswer_does_not_mutate_a_reused_graded_assessment()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 2);
        var startedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var completedAt = startedAt.AddMinutes(30);
        var passed = new Assessment
        {
            UserId = 42,
            TrainingModule = module.Name,
            Score = 75,
            Passed = true,
            StartedAt = startedAt,
            CompletedAt = completedAt,
        };
        dbContext.Assessments.Add(passed);
        await dbContext.SaveChangesAsync();
        var service = CreateService(dbContext);

        await service.SubmitAnswerAsync(42, module, module.PageByName("summative-q1")!, "1");
        var result = await service.SubmitAnswerAsync(42, module, module.PageByName("summative-q2")!, "1");

        Assert.NotNull(result.GradedAssessment);
        var persisted = await dbContext.Assessments.AsNoTracking().SingleAsync();
        Assert.Equal(75, persisted.Score);
        Assert.True(persisted.Passed);
        Assert.Equal(startedAt, persisted.StartedAt);
        Assert.Equal(completedAt, persisted.CompletedAt);
    }

    [Fact]
    public async Task SubmitAnswer_keeps_text_option_compatibility_but_stores_numeric_id()
    {
        await using var dbContext = CreateDbContext();
        var module = CreateModule(summativeCount: 1);
        var service = CreateService(dbContext);

        var result = await service.SubmitAnswerAsync(1, module, module.PageByName("formative-q")!, CorrectAnswer);

        Assert.True(result.IsCorrect);
        Assert.Equal(1, result.AnswerId);
        Assert.Equal("1", Assert.Single((await dbContext.Responses.SingleAsync()).Answers));
        Assert.Empty(await dbContext.Assessments.ToListAsync());
    }

    [Fact]
    public async Task Multi_select_accepts_answers_in_any_order_and_persists_deterministic_numeric_ids()
    {
        await using var dbContext = CreateDbContext();
        var question = MultiSelectQuestion("formative-multi");
        var module = CreateModuleWithQuestion(question);
        var service = CreateService(dbContext);

        var result = await service.SubmitAnswerAsync(1, module, question, ["2", "1"]);

        Assert.True(question.IsMultiSelect);
        Assert.True(result.IsCorrect);
        Assert.Equal([1, 2], result.AnswerIds);
        Assert.Equal(["1", "2"], Assert.Single(await dbContext.Responses.ToListAsync()).Answers);
    }

    [Theory]
    [InlineData("1", "3")]
    [InlineData("1", "")]
    public async Task Multi_select_requires_exactly_the_correct_set(string first, string second)
    {
        await using var dbContext = CreateDbContext();
        var question = MultiSelectQuestion("formative-multi");
        var module = CreateModuleWithQuestion(question);
        var service = CreateService(dbContext);
        var answers = string.IsNullOrEmpty(second) ? [first] : new[] { first, second };

        var result = await service.SubmitAnswerAsync(1, module, question, answers);

        Assert.True(result.IsValid);
        Assert.False(result.IsCorrect);
    }

    [Fact]
    public async Task Multi_select_rejects_missing_answers()
    {
        await using var dbContext = CreateDbContext();
        var question = MultiSelectQuestion("formative-multi");
        var module = CreateModuleWithQuestion(question);
        var service = CreateService(dbContext);

        var result = await service.SubmitAnswerAsync(1, module, question, []);

        Assert.False(result.IsValid);
        Assert.Empty(await dbContext.Responses.ToListAsync());
    }

    [Fact]
    public async Task Multi_select_summative_answer_is_graded_and_uses_exact_set_scoring()
    {
        await using var dbContext = CreateDbContext();
        var question = MultiSelectQuestion("summative-multi") with { PageType = "summative" };
        var module = CreateModuleWithQuestion(question);
        var service = CreateService(dbContext);

        var result = await service.SubmitAnswerAsync(1, module, question, ["1", "2"]);

        Assert.True(result.IsCorrect);
        Assert.NotNull(result.GradedAssessment);
        Assert.Equal(100f, result.GradedAssessment!.Score);
        Assert.True(result.GradedAssessment.Passed);
    }

    [Fact]
    public void Multi_select_is_inferred_from_two_correct_options()
    {
        Assert.True(MultiSelectQuestion("multi").IsMultiSelect);
        Assert.False(SummativeQuestion("single").IsMultiSelect);
    }

    private static TrainingPageContent MultiSelectQuestion(string name) =>
        new(name, "formative", "Multi-select", string.Empty,
            [
                new QuestionAnswerOption("Correct one", true),
                new QuestionAnswerOption("Correct two", true),
                new QuestionAnswerOption("Wrong", false),
            ],
            "Well done", "Try again");

    private static TrainingModuleContent CreateModuleWithQuestion(TrainingPageContent question) =>
        new(
            Name: "module-multi",
            Title: "Multi-select module",
            Description: string.Empty,
            Outcomes: string.Empty,
            Criteria: string.Empty,
            Duration: 1,
            Position: 1,
            Live: true,
            Pages: [question, TrainingPageContent.CreatePage("results", "assessment_results", "Results", string.Empty)]);
}
