using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Web.Services;

public sealed class QuestionnaireEventTracker(AuthenticatedKpiEventWriter eventWriter)
{
    public const string AssessmentStartEvent = "summative_assessment_start";
    public const string AnswerEvent = "questionnaire_answer";
    public const string FeedbackStartEvent = "feedback_start";
    public const string ConfidenceCompleteEvent = "confidence_check_complete";

    public async Task TrackConfidenceCompleteAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent page,
        CancellationToken cancellationToken)
    {
        var existing = await eventWriter.ListNamedEventsAsync(userId, ConfidenceCompleteEvent, cancellationToken);
        if (existing.Any(item => PropertyEquals(item.Properties, "training_module_id", module.Name)))
        {
            return;
        }

        var properties = RouteProperties(module, page);
        properties["type"] = page.PageType;
        await eventWriter.TrackAsync(
            context,
            userId,
            ConfidenceCompleteEvent,
            "training/pages",
            "show",
            cancellationToken,
            properties);
    }

    public async Task TrackFeedbackStartAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        CancellationToken cancellationToken)
    {
        var existing = await eventWriter.ListNamedEventsAsync(userId, FeedbackStartEvent, cancellationToken);
        if (existing.Any(item => PropertyEquals(item.Properties, "training_module_id", module.Name)))
        {
            return;
        }

        await eventWriter.TrackAsync(
            context,
            userId,
            FeedbackStartEvent,
            "training/questions",
            "show",
            cancellationToken,
            RouteProperties(module, question));
    }

    public async Task TrackAssessmentStartAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        CancellationToken cancellationToken)
    {
        var existing = await eventWriter.ListNamedEventsAsync(userId, AssessmentStartEvent, cancellationToken);
        if (existing.Any(item => PropertyEquals(item.Properties, "training_module_id", module.Name)))
        {
            return;
        }

        await eventWriter.TrackAsync(
            context,
            userId,
            AssessmentStartEvent,
            "training/questions",
            "show",
            cancellationToken,
            RouteProperties(module, question));
    }

    public Task TrackAnswerAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        int answerId,
        bool success,
        CancellationToken cancellationToken)
        => TrackAnswerAsync(context, userId, module, question, [answerId], success, cancellationToken);

    public Task TrackAnswerAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent question,
        IReadOnlyList<int> answerIds,
        bool success,
        CancellationToken cancellationToken)
    {
        var properties = RouteProperties(module, question);
        properties["type"] = question.PageType;
        properties["success"] = success;
        properties["answers"] = answerIds.ToArray();
        return eventWriter.TrackAsync(
            context,
            userId,
            AnswerEvent,
            "training/responses",
            "update",
            cancellationToken,
            properties);
    }

    private static Dictionary<string, object?> RouteProperties(
        TrainingModuleContent module,
        TrainingPageContent question) => new()
    {
        ["training_module_id"] = module.Name,
        ["id"] = question.Name,
        ["uid"] = question.ContentId ?? question.Name,
        ["mod_uid"] = module.ContentId ?? module.Name,
    };

    private static bool PropertyEquals(
        IReadOnlyDictionary<string, object?> properties,
        string key,
        string expected) =>
        properties.TryGetValue(key, out var value)
        && string.Equals(
            value is JsonElement { ValueKind: JsonValueKind.String } element
                ? element.GetString()
                : value?.ToString(),
            expected,
            StringComparison.Ordinal);
}
