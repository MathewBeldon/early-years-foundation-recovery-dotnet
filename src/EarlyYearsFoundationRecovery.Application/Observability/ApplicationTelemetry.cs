using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace EarlyYearsFoundationRecovery.Application.Observability;

public static class ApplicationTelemetry
{
    public const string ActivitySourceName = "EarlyYearsFoundationRecovery.Application";
    public const string MeterName = "EarlyYearsFoundationRecovery.Application";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> AuthEvents = Meter.CreateCounter<long>(
        "eyfr.auth.events",
        description: "Authentication journey events.");

    private static readonly Counter<long> RegistrationStepSubmissions = Meter.CreateCounter<long>(
        "eyfr.registration.step.submissions",
        description: "Registration step submissions by step and outcome.");

    private static readonly Counter<long> FeedbackSubmissions = Meter.CreateCounter<long>(
        "eyfr.feedback.submissions",
        description: "Feedback submissions by outcome.");

    private static readonly Counter<long> AccountClosureEvents = Meter.CreateCounter<long>(
        "eyfr.account_closure.events",
        description: "Account closure journey events.");

    public static Activity? StartJourneyActivity(string name, string journey, string? step = null)
    {
        var activity = ActivitySource.StartActivity(name, ActivityKind.Internal);
        activity?.SetTag("app.journey", journey);
        if (!string.IsNullOrWhiteSpace(step))
        {
            activity?.SetTag("app.step", step);
        }

        return activity;
    }

    public static void RecordAuthEvent(string eventName, string result, string? reason = null) =>
        AuthEvents.Add(1, Tags(
            ("auth.event", eventName),
            ("auth.result", result),
            ("auth.reason", reason)));

    public static void RecordRegistrationStep(string step, string result, string mode, string? nextStep = null) =>
        RegistrationStepSubmissions.Add(1, Tags(
            ("registration.step", step),
            ("registration.result", result),
            ("registration.mode", mode),
            ("registration.next_step", nextStep)));

    public static void RecordFeedbackSubmission(string result, string mode, string? next = null) =>
        FeedbackSubmissions.Add(1, Tags(
            ("feedback.result", result),
            ("feedback.mode", mode),
            ("feedback.next", next)));

    public static void RecordAccountClosureEvent(string eventName, string result, string? reason = null) =>
        AccountClosureEvents.Add(1, Tags(
            ("account_closure.event", eventName),
            ("account_closure.result", result),
            ("account_closure.reason", reason)));

    public static void MarkActivityFailure(Activity? activity, string reason)
    {
        activity?.SetTag("app.result", "failed");
        activity?.SetTag("app.failure_reason", reason);
        activity?.SetStatus(ActivityStatusCode.Error, reason);
    }

    public static void MarkActivitySuccess(Activity? activity)
    {
        activity?.SetTag("app.result", "succeeded");
        activity?.SetStatus(ActivityStatusCode.Ok);
    }

    private static KeyValuePair<string, object?>[] Tags(params (string Key, object? Value)[] tags) =>
        tags
            .Where(tag => tag.Value is not null && !string.IsNullOrWhiteSpace(tag.Value.ToString()))
            .Select(tag => new KeyValuePair<string, object?>(tag.Key, tag.Value))
            .ToArray();
}
