using System.Text;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[TypeFilter(typeof(RequireRegistrationCompleteFilter))]
[Route("my-account/learning-log")]
public class LearningLogController(
    INoteRepository notes,
    ITrainingContentProvider contentProvider,
    IUserModuleProgressRepository progressRepository,
    AuthenticatedKpiEventWriter kpiEvents) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Show(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");
        var modules = await GetActiveModulesAsync(userId, cancellationToken);
        var model = new LearningLogViewModel
        {
            Modules = await BuildModuleTabsAsync(userId, modules, cancellationToken),
        };

        return View(model);
    }

    // Rails v1.5.0 ac546721 config/routes.rb resource :notes, only: %i[show create update]
    // maps POST to create and PATCH/PUT to update. Both actions upsert by module+page.
    [HttpPost("")]
    [HttpPatch("")]
    [HttpPut("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(LearningLogNoteFormViewModel form, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId() ?? throw new InvalidOperationException("User is not authenticated.");

        var existing = await notes.GetByUserAndPageAsync(userId, form.TrainingModule, form.Name, cancellationToken);
        var note = existing ?? new Note
        {
            UserId = userId,
            TrainingModule = form.TrainingModule,
            Name = form.Name,
        };

        note.Title = form.Title;
        note.Body = form.Body;

        await notes.SaveAsync(note, cancellationToken);

        // Rails names telemetry from the routed action: POST/create emits
        // created even when it upserts an existing row; PATCH/PUT emit updated.
        var isCreateAction = HttpMethods.IsPost(Request.Method);
        var eventName = isCreateAction ? "user_note_created" : "user_note_updated";
        var eventAction = isCreateAction ? "create" : "update";
        var eventProperties = new Dictionary<string, object?>
        {
            ["length"] = CountUnicodeCodePoints(form.Body),
            ["title"] = form.Title,
            ["training_module"] = form.TrainingModule,
            ["name"] = form.Name,
        };
        if (form.NextPageName is not null)
        {
            eventProperties["next_page_name"] = form.NextPageName;
        }
        if (form.NextPageModule is not null)
        {
            eventProperties["next_page_module"] = form.NextPageModule;
        }

        await kpiEvents.TrackAsync(
            HttpContext,
            userId,
            eventName,
            "training/notes",
            eventAction,
            cancellationToken,
            eventProperties);

        return Redirect(LearningLogRedirect.ResolveNextPagePath(
            form.NextPageUrl,
            form.NextPageModule,
            form.NextPageName,
            form.TrainingModule,
            form.Name));
    }

    private static int CountUnicodeCodePoints(string? value) =>
        value?.EnumerateRunes().Count() ?? 0;

    private async Task<IReadOnlyList<TrainingModuleContent>> GetActiveModulesAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var progress = await progressRepository.GetForUserAsync(userId, cancellationToken);
        var startedModuleNames = progress
            .Where(p => p.StartedAt is not null)
            .Select(p => p.ModuleName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var liveModules = await contentProvider.GetLiveModulesAsync(cancellationToken);
        return liveModules
            .Where(m => startedModuleNames.Contains(m.Name))
            .ToList();
    }

    private async Task<IReadOnlyList<LearningLogModuleTabViewModel>> BuildModuleTabsAsync(
        long userId,
        IReadOnlyList<TrainingModuleContent> modules,
        CancellationToken cancellationToken)
    {
        var allNotes = await notes.GetByUserAndModulesAsync(
            userId,
            modules.Select(m => m.Name),
            cancellationToken);
        var notesByModule = allNotes
            .GroupBy(n => n.TrainingModule ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var tabs = new List<LearningLogModuleTabViewModel>();

        foreach (var module in modules)
        {
            notesByModule.TryGetValue(module.Name, out var moduleNotes);
            tabs.Add(new LearningLogModuleTabViewModel
            {
                Name = module.Name,
                Title = module.Title,
                TabLabel = module.TabLabel,
                TabAnchor = module.TabAnchor,
                Notes = (moduleNotes ?? []).Select(n => new LearningLogNoteViewModel
                {
                    Title = n.Title ?? string.Empty,
                    Body = n.Body ?? string.Empty,
                    LoggedAt = n.UpdatedAt.ToString("d MMMM yyyy"),
                    PageUrl = $"/modules/{n.TrainingModule}/content-pages/{n.Name}",
                    Filled = IsNoteFilled(n.Body),
                }).ToList(),
            });
        }

        return tabs;
    }

    private static bool IsNoteFilled(string? body) =>
        !string.IsNullOrWhiteSpace(body);
}
