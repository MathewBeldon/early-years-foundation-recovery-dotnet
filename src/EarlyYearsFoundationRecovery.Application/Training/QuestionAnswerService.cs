using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed class QuestionAnswerService(
    ITrainingAssessmentRepository assessmentRepository,
    AssessmentProgressService assessmentProgressService)
{
    public const float PassThreshold = 70f;

    public async Task<QuestionAnswerResult> SubmitAnswerAsync(
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        string selectedAnswer,
        CancellationToken cancellationToken = default)
    {
        var option = question.Answers.FirstOrDefault(a =>
            string.Equals(a.Text, selectedAnswer, StringComparison.Ordinal));

        if (option is null)
        {
            return QuestionAnswerResult.Invalid("Please select an answer.");
        }

        var isCorrect = option.Correct;
        long? assessmentId = null;

        if (question.IsSummative)
        {
            var assessment = await assessmentProgressService.ResolveSummativeAssessmentAsync(
                userId,
                module.Name,
                cancellationToken);
            assessmentId = assessment.Id;
        }

        var existing = question.IsSummative && assessmentId is not null
            ? await assessmentRepository.GetResponseForAssessmentAsync(
                userId,
                module.Name,
                question.Name,
                assessmentId.Value,
                cancellationToken)
            : await assessmentRepository.GetResponseAsync(userId, module.Name, question.Name, cancellationToken);
        if (existing is not null && question.IsFormative)
        {
            return QuestionAnswerResult.FromExisting(existing, question);
        }

        var response = existing ?? new Response
        {
            UserId = userId,
            TrainingModule = module.Name,
            QuestionName = question.Name,
            QuestionType = question.PageType,
        };

        response.Answers = [selectedAnswer];
        response.Correct = isCorrect;
        response.AssessmentId = assessmentId;
        response.UpdatedAt = DateTime.UtcNow;
        if (existing is null)
        {
            response.CreatedAt = DateTime.UtcNow;
        }

        await assessmentRepository.SaveResponseAsync(response, cancellationToken);

        Assessment? gradedAssessment = null;
        if (question.IsSummative && module.IsLastSummativeQuestion(question.Name) && assessmentId is not null)
        {
            gradedAssessment = await GradeAssessmentAsync(userId, module, assessmentId.Value, cancellationToken);
        }

        return new QuestionAnswerResult(
            IsValid: true,
            IsCorrect: isCorrect,
            FeedbackMessage: isCorrect ? question.SuccessMessage : question.FailureMessage,
            GradedAssessment: gradedAssessment);
    }

    public async Task<Assessment?> GradeAssessmentAsync(
        long userId,
        TrainingModuleContent module,
        long assessmentId,
        CancellationToken cancellationToken = default)
    {
        var assessment = await assessmentRepository.GetLatestAssessmentAsync(userId, module.Name, cancellationToken: cancellationToken);
        if (assessment is null || assessment.Id != assessmentId)
        {
            return null;
        }

        var responses = await assessmentRepository.GetResponsesForAssessmentAsync(assessmentId, cancellationToken);
        var totalQuestions = module.SummativeQuestions.Count;
        if (totalQuestions == 0)
        {
            return assessment;
        }

        var correctCount = responses.Count(r => r.Correct == true);
        var score = (float)Math.Round(correctCount * 100.0 / totalQuestions, 1);
        assessment.Score = score;
        assessment.Passed = score >= PassThreshold;
        assessment.CompletedAt = DateTime.UtcNow;
        await assessmentRepository.SaveAssessmentAsync(assessment, cancellationToken);
        return assessment;
    }

    public async Task<Response?> GetExistingResponseAsync(
        long userId,
        string moduleName,
        string questionName,
        CancellationToken cancellationToken = default) =>
        await assessmentRepository.GetResponseAsync(userId, moduleName, questionName, cancellationToken);
}

public sealed record QuestionAnswerResult(
    bool IsValid,
    bool? IsCorrect = null,
    string? FeedbackMessage = null,
    string? ErrorMessage = null,
    Assessment? GradedAssessment = null)
{
    public static QuestionAnswerResult Invalid(string message) => new(false, ErrorMessage: message);

    public static QuestionAnswerResult FromExisting(Response response, TrainingPageContent question) =>
        new(
            true,
            response.Correct,
            response.Correct == true ? question.SuccessMessage : question.FailureMessage);

    public bool ShowFeedback => IsValid && IsCorrect is not null;
}
