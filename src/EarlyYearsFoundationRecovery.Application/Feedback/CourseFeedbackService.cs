using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Feedback;

public sealed class CourseFeedbackService(
    IFeedbackContentProvider contentProvider,
    ICourseFeedbackRepository feedbackRepository,
    IUserRepository users)
{
    public async Task<bool> IsCompleteAsync(long userId, CancellationToken cancellationToken = default)
    {
        var form = await contentProvider.GetFormAsync(cancellationToken);
        var count = await feedbackRepository.CountResponsesAsync(userId, cancellationToken);
        return count >= form.FeedbackQuestions.Count;
    }

    public Task<Response?> GetResponseAsync(long userId, string questionName, CancellationToken cancellationToken = default) =>
        feedbackRepository.GetResponseAsync(userId, questionName, cancellationToken);

    public async Task<SaveFeedbackResult> SaveResponseAsync(
        long userId,
        FeedbackQuestionContent question,
        IReadOnlyList<string> selectedAnswers,
        string? textInput,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateResponse(question, selectedAnswers, textInput, out var error))
        {
            return SaveFeedbackResult.Invalid(error);
        }

        var existing = await feedbackRepository.GetResponseAsync(userId, question.Name, cancellationToken);
        var response = existing ?? new Response
        {
            UserId = userId,
            TrainingModule = FeedbackFormContent.ModuleName,
            QuestionName = question.Name,
            QuestionType = "feedback",
            CreatedAt = DateTime.UtcNow,
        };

        response.Answers = selectedAnswers.ToList();
        response.TextInput = string.IsNullOrWhiteSpace(textInput) ? null : textInput.Trim();
        response.Correct = true;
        response.UpdatedAt = DateTime.UtcNow;

        await feedbackRepository.SaveResponseAsync(response, cancellationToken);

        if (question.Skippable)
        {
            await UpdateResearchParticipantAsync(userId, selectedAnswers, cancellationToken);
        }

        return SaveFeedbackResult.Success();
    }

    private async Task UpdateResearchParticipantAsync(
        long userId,
        IReadOnlyList<string> selectedAnswers,
        CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return;
        }

        user.ResearchParticipant = selectedAnswers.Contains("1");
        await users.SaveAsync(user, cancellationToken);
    }

    private static bool ValidateResponse(
        FeedbackQuestionContent question,
        IReadOnlyList<string> selectedAnswers,
        string? textInput,
        out string error)
    {
        if (question.InputType == "textarea")
        {
            if (string.IsNullOrWhiteSpace(textInput))
            {
                error = "Enter your answer.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        if (selectedAnswers.Count == 0)
        {
            error = "Select an answer.";
            return false;
        }

        if (question.HasOther && selectedAnswers.Contains(GetOtherIndex(question).ToString())
            && string.IsNullOrWhiteSpace(textInput))
        {
            error = "Enter details for your Other answer.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int GetOtherIndex(FeedbackQuestionContent question) =>
        Math.Max(question.Options.Count - 1, 0);
}

public sealed record SaveFeedbackResult(bool IsValid, string? ErrorMessage = null)
{
    public static SaveFeedbackResult Success() => new(true);
    public static SaveFeedbackResult Invalid(string message) => new(false, message);
}
