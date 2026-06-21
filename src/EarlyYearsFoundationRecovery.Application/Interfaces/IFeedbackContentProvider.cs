namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IFeedbackContentProvider
{
    Task<FeedbackFormContent> GetFormAsync(CancellationToken cancellationToken = default);
    Task<FeedbackQuestionContent?> GetQuestionAsync(string questionName, CancellationToken cancellationToken = default);
}

public sealed record FeedbackFormContent(IReadOnlyList<FeedbackQuestionContent> Questions)
{
    public const string ModuleName = "course";

    public IReadOnlyList<FeedbackQuestionContent> FeedbackQuestions =>
        Questions.Where(q => q.IsFeedbackQuestion).ToList();

    public FeedbackQuestionContent? PageByName(string name) =>
        Questions.FirstOrDefault(q => string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase));

    public FeedbackQuestionContent? FirstQuestion => FeedbackQuestions.FirstOrDefault();

    public FeedbackQuestionContent? SkippableQuestion =>
        FeedbackQuestions.FirstOrDefault(q => q.Skippable);

    public FeedbackQuestionContent? ThankYouPage =>
        Questions.FirstOrDefault(q => q.IsThankYou);

    public FeedbackQuestionContent? NextAfter(string questionName)
    {
        var questions = FeedbackQuestions.ToList();
        var index = questions.FindIndex(q => string.Equals(q.Name, questionName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < questions.Count - 1 ? questions[index + 1] : ThankYouPage;
    }

    public FeedbackQuestionContent? PreviousBefore(string questionName)
    {
        var questions = FeedbackQuestions.ToList();
        var index = questions.FindIndex(q => string.Equals(q.Name, questionName, StringComparison.OrdinalIgnoreCase));
        return index > 0 ? questions[index - 1] : null;
    }
}

public sealed record FeedbackQuestionContent(
    string Name,
    string PageType,
    string InputType,
    string Heading,
    string Legend,
    string Body,
    IReadOnlyList<string> Options,
    bool Skippable = false,
    bool HasOther = false,
    string? OtherLabel = null,
    bool HasMore = false,
    bool HasOr = false,
    string? OrLabel = null)
{
    public bool IsThankYou => PageType is "thank_you" or "text_page" || Name == "thank-you";
    public bool IsFeedbackQuestion => PageType == "feedback";
}
