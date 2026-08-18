using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed record QuestionAnswerDisplayOption(
    string Text,
    bool Correct,
    bool Checked,
    bool Disabled,
    string? StatusHint,
    bool EmphasiseLabel);

public static class FormativeQuestionDisplay
{
    public static IReadOnlyList<QuestionAnswerDisplayOption> BuildAnswerOptions(
        TrainingPageContent question,
        string? selectedAnswer,
        bool responded = false) =>
        BuildAnswerOptions(question, selectedAnswer is null ? [] : [selectedAnswer], responded);

    public static IReadOnlyList<QuestionAnswerDisplayOption> BuildAnswerOptions(
        TrainingPageContent question,
        IReadOnlyCollection<string> selectedAnswers,
        bool responded = false) =>
        question.Answers.Select((answer, optionIndex) =>
        {
            var numericId = (optionIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var isSelected = selectedAnswers.Contains(numericId, StringComparer.Ordinal)
                || selectedAnswers.Contains(answer.Text, StringComparer.Ordinal);
            var statusHint = responded ? ResolveReviewHint(answer.Correct, isSelected) : null;

            return new QuestionAnswerDisplayOption(
                Text: answer.Text,
                Correct: answer.Correct,
                Checked: isSelected,
                Disabled: responded,
                StatusHint: statusHint,
                EmphasiseLabel: responded && (answer.Correct || isSelected));
        }).ToList();

    private static string? ResolveReviewHint(bool correct, bool selected)
    {
        if (correct)
        {
            return "This is the correct answer";
        }

        if (selected)
        {
            return "You selected this answer";
        }

        return null;
    }

    public static (string BannerTitle, string? BannerCssClass) BuildBanner(bool? isCorrect) =>
        isCorrect == true
            ? ("That's right", "govuk-notification-banner--success")
            : ("That's not quite right", null);

    public static string ResolveSubmitLabel(TrainingPageContent question) =>
        question.IsSummative ? "Save and continue" : "Next";
}
