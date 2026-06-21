using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class FormativeQuestionDisplayTests
{
    private static TrainingPageContent CreateFormativeQuestion() =>
        new(
            "check-understanding",
            "formative",
            "Formative check",
            "Pick the correct answer.",
            [
                new QuestionAnswerOption("Correct answer", true),
                new QuestionAnswerOption("Wrong answer", false),
            ],
            "Correct — well done.",
            "Wrong — try again.");

    [Fact]
    public void BuildAnswerOptions_whenAnsweredWrong_marks_selection_and_correct_answer()
    {
        var question = CreateFormativeQuestion();
        var options = FormativeQuestionDisplay.BuildAnswerOptions(question, "Wrong answer", responded: true);

        var selected = options.Single(option => option.Text == "Wrong answer");
        var correct = options.Single(option => option.Text == "Correct answer");

        Assert.True(selected.Checked);
        Assert.Equal("You selected this answer", selected.StatusHint);
        Assert.True(selected.EmphasiseLabel);

        Assert.False(correct.Checked);
        Assert.Equal("This is the correct answer", correct.StatusHint);
        Assert.True(correct.EmphasiseLabel);
    }

    [Fact]
    public void BuildAnswerOptions_whenAnsweredCorrectly_uses_plain_text_hint()
    {
        var question = CreateFormativeQuestion();
        var options = FormativeQuestionDisplay.BuildAnswerOptions(question, "Correct answer", responded: true);

        var selected = options.Single(option => option.Text == "Correct answer");
        var wrong = options.Single(option => option.Text == "Wrong answer");

        Assert.Equal("This is the correct answer", selected.StatusHint);
        Assert.True(selected.EmphasiseLabel);
        Assert.Null(wrong.StatusHint);
        Assert.False(wrong.EmphasiseLabel);
    }

    [Fact]
    public void BuildBanner_uses_live_copy()
    {
        Assert.Equal("That's right", FormativeQuestionDisplay.BuildBanner(true).BannerTitle);
        Assert.Equal("That's not quite right", FormativeQuestionDisplay.BuildBanner(false).BannerTitle);
    }
}
