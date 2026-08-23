namespace EarlyYearsFoundationRecovery.Web.Services;

/// <summary>
/// Mirrors Rails v1.5.0 FeedbackController's authenticated event lifecycle.
/// Course feedback start and completion are each recorded at most once per user,
/// irrespective of module or visit.
/// </summary>
public sealed class CourseFeedbackEventTracker(AuthenticatedKpiEventWriter eventWriter)
{
    public const string StartEvent = "feedback_start";
    public const string CompleteEvent = "feedback_complete";

    public Task TrackStartAsync(
        HttpContext context,
        long userId,
        string questionName,
        CancellationToken cancellationToken) =>
        TrackOnceAsync(context, userId, StartEvent, "update", questionName, cancellationToken);

    public Task TrackCompleteAsync(
        HttpContext context,
        long userId,
        string pageName,
        CancellationToken cancellationToken) =>
        TrackOnceAsync(context, userId, CompleteEvent, "show", pageName, cancellationToken);

    private async Task TrackOnceAsync(
        HttpContext context,
        long userId,
        string eventName,
        string railsAction,
        string contentName,
        CancellationToken cancellationToken)
    {
        if ((await eventWriter.ListNamedEventsAsync(userId, eventName, cancellationToken)).Count != 0)
        {
            return;
        }

        await eventWriter.TrackAsync(
            context,
            userId,
            eventName,
            "feedback",
            railsAction,
            cancellationToken,
            new Dictionary<string, object?> { ["id"] = contentName });
    }
}
