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
        => await SubmitAnswerAsync(userId, module, question, [selectedAnswer], cancellationToken);

    public async Task<QuestionAnswerResult> SubmitAnswerAsync(
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        IReadOnlyList<string> selectedAnswers,
        CancellationToken cancellationToken = default,
        string? textInput = null)
    {
        var optionIndexes = ResolveOptionIndexes(question, selectedAnswers);

        if (optionIndexes.Count == 0 || (!question.IsMultiSelect && optionIndexes.Count != 1))
        {
            return QuestionAnswerResult.Invalid(question.IsFeedback ? "Please select an answer" : "Please select an answer.");
        }

        var answerIds = optionIndexes.Select(index => index + 1).ToArray();
        var correctIndexes = question.Answers
            .Select((option, index) => (option, index))
            .Where(item => item.option.Correct)
            .Select(item => item.index)
            .ToHashSet();
        var isCorrect = question.IsFeedback || optionIndexes.ToHashSet().SetEquals(correctIndexes);
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
                question.PageType,
                cancellationToken)
            : await assessmentRepository.GetResponseAsync(
                userId,
                module.Name,
                question.Name,
                question.PageType,
                cancellationToken);
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

        response.Answers = answerIds
            .Select(id => id.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
        response.Correct = isCorrect;
        response.TextInput = string.IsNullOrWhiteSpace(textInput) ? null : textInput.Trim();
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
            GradedAssessment: gradedAssessment,
            AnswerId: answerIds[0],
            AnswerIds: answerIds);
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
        if (totalQuestions == 0 || AssessmentProgressService.IsGraded(assessment))
        {
            return assessment;
        }

        var correctCount = responses.Count(r => r.QuestionType == "summative" && r.Correct == true);
        var score = (float)(correctCount * 100.0 / totalQuestions);
        assessment.Score = score;
        assessment.Passed = score >= PassThreshold;
        assessment.CompletedAt = DateTime.UtcNow;
        await assessmentRepository.SaveAssessmentAsync(assessment, cancellationToken);
        return assessment;
    }

    public async Task<Response?> GetExistingResponseAsync(
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        CancellationToken cancellationToken = default) =>
        question.IsSummative
            ? await assessmentRepository.GetResponseForAssessmentAsync(
                userId,
                module.Name,
                question.Name,
                (await assessmentProgressService.ResolveSummativeAssessmentAsync(
                    userId,
                    module.Name,
                    cancellationToken)).Id,
                question.PageType,
                cancellationToken)
            : await assessmentRepository.GetResponseAsync(
                userId,
                module.Name,
                question.Name,
                question.PageType,
                cancellationToken);

    private static IReadOnlyList<int> ResolveOptionIndexes(
        TrainingPageContent question,
        IReadOnlyList<string> selectedAnswers)
    {
        var indexes = new HashSet<int>();
        foreach (var selectedAnswer in selectedAnswers)
        {
            var optionIndex = ResolveOptionIndex(question, selectedAnswer);
            if (optionIndex < 0)
            {
                return [];
            }

            indexes.Add(optionIndex);
        }

        return indexes.OrderBy(index => index).ToArray();
    }

    private static int ResolveOptionIndex(TrainingPageContent question, string selectedAnswer)
    {
        if (int.TryParse(selectedAnswer, out var answerId)
            && answerId >= 1
            && answerId <= question.Answers.Count
            && string.Equals(
                selectedAnswer,
                answerId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                StringComparison.Ordinal))
        {
            return answerId - 1;
        }

        return question.Answers.ToList().FindIndex(option =>
            string.Equals(option.Text, selectedAnswer, StringComparison.Ordinal));
    }
}

public sealed record QuestionAnswerResult(
    bool IsValid,
    bool? IsCorrect = null,
    string? FeedbackMessage = null,
    string? ErrorMessage = null,
    Assessment? GradedAssessment = null,
    int? AnswerId = null,
    IReadOnlyList<int>? AnswerIds = null)
{
    public static QuestionAnswerResult Invalid(string message) => new(false, ErrorMessage: message);

    public static QuestionAnswerResult FromExisting(Response response, TrainingPageContent question) =>
        new(
            true,
            response.Correct,
            response.Correct == true ? question.SuccessMessage : question.FailureMessage,
            AnswerId: response.Answers
                .Select(answer => int.TryParse(answer, out var id) ? (int?)id : null)
                .FirstOrDefault(id => id is not null),
            AnswerIds: response.Answers
                .Select(answer => int.TryParse(answer, out var id) ? (int?)id : null)
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .ToArray());

    public bool ShowFeedback => IsValid && IsCorrect is not null;
}
