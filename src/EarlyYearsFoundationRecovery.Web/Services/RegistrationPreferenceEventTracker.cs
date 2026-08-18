namespace EarlyYearsFoundationRecovery.Web.Services;

/// <summary>
/// Rails v1.5.0 ac546721 registration preference events. The event properties
/// deliberately retain the Rails tracking shape: path, controller, action and
/// the boolean success result.
/// </summary>
public sealed class RegistrationPreferenceEventTracker(AuthenticatedKpiEventWriter eventWriter)
{
    public const string TrainingEmailsEvent = "user_training_emails_change";
    public const string ResearchParticipantEvent = "user_research_participant_change";

    public Task TrackTrainingEmailsAsync(
        HttpContext httpContext,
        long userId,
        bool success,
        CancellationToken cancellationToken = default) =>
        TrackAsync(
            httpContext,
            userId,
            TrainingEmailsEvent,
            "registration/training_emails",
            success,
            cancellationToken);

    public Task TrackResearchParticipantAsync(
        HttpContext httpContext,
        long userId,
        bool success,
        CancellationToken cancellationToken = default) =>
        TrackAsync(
            httpContext,
            userId,
            ResearchParticipantEvent,
            "registration/research_participants",
            success,
            cancellationToken);

    private Task TrackAsync(
        HttpContext httpContext,
        long userId,
        string eventName,
        string controller,
        bool success,
        CancellationToken cancellationToken) =>
        eventWriter.TrackAsync(
            httpContext,
            userId,
            eventName,
            controller,
            "update",
            cancellationToken,
            new Dictionary<string, object?> { ["success"] = success });
}
