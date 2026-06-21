using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[TypeFilter(typeof(RequireRegistrationCompleteFilter))]
[Route("my-account/learning-log")]
public class LearningLogController(
    INoteRepository notes,
    ITrainingContentProvider contentProvider,
    IUserModuleProgressRepository progressRepository) : Controller
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

    [HttpPost("")]
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

        return Redirect(ResolveNextPagePath(form));
    }

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

    private static string ResolveNextPagePath(LearningLogNoteFormViewModel form)
    {
        if (!string.IsNullOrWhiteSpace(form.NextPageUrl))
        {
            return form.NextPageUrl;
        }

        if (!string.IsNullOrWhiteSpace(form.NextPageModule) && !string.IsNullOrWhiteSpace(form.NextPageName))
        {
            return $"/modules/{form.NextPageModule}/content-pages/{form.NextPageName}";
        }

        if (!string.IsNullOrWhiteSpace(form.TrainingModule) && !string.IsNullOrWhiteSpace(form.Name))
        {
            return $"/modules/{form.TrainingModule}/content-pages/{form.Name}";
        }

        return "/my-account/learning-log";
    }

    private static bool IsNoteFilled(string? body) =>
        !string.IsNullOrWhiteSpace(body);
}
