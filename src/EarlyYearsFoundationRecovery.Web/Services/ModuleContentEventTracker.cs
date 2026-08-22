using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Web.Services;

/// <summary>Rails v1.5.0 KPI events owned by Training::ModulesController and Training::PagesController.</summary>
public sealed class ModuleContentEventTracker(AuthenticatedKpiEventWriter eventWriter)
{
    public const string OverviewEvent = "module_overview_page";
    public const string StartEvent = "module_start";

    public Task TrackOverviewAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        CancellationToken cancellationToken) =>
        eventWriter.TrackAsync(
            context,
            userId,
            OverviewEvent,
            "training/modules",
            "show",
            cancellationToken,
            new Dictionary<string, object?>
            {
                ["id"] = module.Name,
                ["mod_uid"] = module.ContentId ?? module.Name,
            });

    public async Task TrackStartAsync(
        HttpContext context,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent page,
        CancellationToken cancellationToken)
    {
        var existing = await eventWriter.ListNamedEventsAsync(userId, StartEvent, cancellationToken);
        if (existing.Any(item => PropertyEquals(item.Properties, "training_module_id", module.Name)))
        {
            return;
        }

        await eventWriter.TrackAsync(
            context,
            userId,
            StartEvent,
            "training/pages",
            "show",
            cancellationToken,
            new Dictionary<string, object?>
            {
                ["training_module_id"] = module.Name,
                ["id"] = page.Name,
                ["type"] = page.PageType,
                ["uid"] = page.ContentId ?? page.Name,
                ["mod_uid"] = module.ContentId ?? module.Name,
            });
    }

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
