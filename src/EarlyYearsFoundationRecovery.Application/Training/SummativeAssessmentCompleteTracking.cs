using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

/// <summary>
/// Rails v1.5.0 commit ac546721 <c>Training::AssessmentsController#track_events</c>
/// records <c>summative_assessment_complete</c> on results show when
/// <c>AssessmentProgress#attempted?</c> and no event exists with
/// <c>training_module_id</c> plus <c>success: true</c>.
/// .NET records only graded attempts (persisted score). Rails writes another
/// failure event on every results GET until a pass is stored; .NET also skips
/// when this module already has an event with the same success value, so
/// repeated failed results views are idempotent.
/// </summary>
public static class SummativeAssessmentCompleteTracking
{
    public const string EventName = "summative_assessment_complete";
    public const string EventType = "summative_assessment";
    public const string RailsController = "training/assessments";
    public const string RailsAction = "show";

    public static bool ShouldRecord(
        Assessment? assessment,
        string moduleName,
        IEnumerable<Event> existingEvents)
    {
        if (!AssessmentProgressService.IsGraded(assessment))
        {
            return false;
        }

        var success = AssessmentProgressService.IsPassed(assessment);
        return !HasEvent(existingEvents, moduleName, success: true)
            && !HasEvent(existingEvents, moduleName, success);
    }

    public static IReadOnlyDictionary<string, object?> CreateProperties(
        TrainingModuleContent module,
        TrainingPageContent resultsPage,
        Assessment assessment) =>
        new Dictionary<string, object?>
        {
            ["type"] = EventType,
            ["training_module_id"] = module.Name,
            ["id"] = resultsPage.Name,
            ["uid"] = resultsPage.ContentId ?? resultsPage.Name,
            ["mod_uid"] = module.ContentId ?? module.Name,
            ["score"] = assessment.Score,
            ["success"] = AssessmentProgressService.IsPassed(assessment),
        };

    public static bool HasEvent(IEnumerable<Event> events, string moduleName, bool success)
    {
        foreach (var existing in events)
        {
            if (!string.Equals(existing.Name, EventName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryGetString(existing.Properties, "training_module_id", out var trainingModuleId)
                || !string.Equals(trainingModuleId, moduleName, StringComparison.Ordinal))
            {
                continue;
            }

            if (TryGetBoolean(existing.Properties, "success", out var recordedSuccess)
                && recordedSuccess == success)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetString(
        IReadOnlyDictionary<string, object?> properties,
        string key,
        out string value)
    {
        value = string.Empty;
        if (!properties.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        value = raw switch
        {
            string text => text,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonElement element => element.ToString(),
            _ => raw.ToString() ?? string.Empty,
        };
        return !string.IsNullOrEmpty(value);
    }

    private static bool TryGetBoolean(
        IReadOnlyDictionary<string, object?> properties,
        string key,
        out bool value)
    {
        value = false;
        if (!properties.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        switch (raw)
        {
            case bool flag:
                value = flag;
                return true;
            case JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False:
                value = element.GetBoolean();
                return true;
            default:
                return false;
        }
    }
}
