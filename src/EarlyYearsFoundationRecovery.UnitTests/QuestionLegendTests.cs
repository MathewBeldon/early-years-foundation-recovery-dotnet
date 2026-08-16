using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class QuestionLegendTests
{
    [Fact]
    public void Summative_single_correct_option_matches_Rails_legend()
    {
        var question = Question("summative", [new("Correct", true), new("Wrong", false)]);

        Assert.Equal("Question body (Select one answer)", QuestionLegend.For(question));
    }

    [Fact]
    public void Summative_multiple_correct_options_keep_the_existing_heading_until_checkbox_support_exists()
    {
        var question = Question("summative", [new("Correct 1", true), new("Correct 2", true)]);

        Assert.Equal("Question 1 of 4", QuestionLegend.For(question));
    }

    [Fact]
    public void Formative_heading_is_unchanged()
    {
        var question = Question("formative", [new("Correct", true)]);

        Assert.Equal("Question 1 of 4", QuestionLegend.For(question));
    }

    private static TrainingPageContent Question(string pageType, IReadOnlyList<QuestionAnswerOption> answers) =>
        new("summative-q1", pageType, "Question 1 of 4", "Question body", answers, null, null);
}
