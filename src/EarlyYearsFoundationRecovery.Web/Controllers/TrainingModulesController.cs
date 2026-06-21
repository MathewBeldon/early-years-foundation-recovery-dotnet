using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Filters;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[TypeFilter(typeof(RequireRegistrationCompleteFilter))]
[Route("modules/{moduleName}")]
public class TrainingModulesController(
    ITrainingContentProvider contentProvider,
    IUserModuleProgressRepository progressRepository,
    ITrainingAssessmentRepository assessmentRepository,
    ModuleProgressService moduleProgressService,
    GovUkMarkdownRenderer markdownRenderer) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Show(string moduleName, CancellationToken cancellationToken)
    {
        var module = await contentProvider.GetModuleByNameAsync(moduleName, cancellationToken);
        if (module is null || !module.Live)
        {
            return NotFound();
        }

        var userId = User.GetUserId()!.Value;
        var progress = await progressRepository.GetAsync(userId, moduleName, asNoTracking: true, cancellationToken);
        var assessment = await assessmentRepository.GetLatestAssessmentAsync(userId, moduleName, asNoTracking: true, cancellationToken);
        var percentage = moduleProgressService.CalculatePercentage(progress, module);
        var (actionLabel, actionUrl) = ModuleProgressDisplay.BuildPrimaryAction(
            module,
            progress,
            assessment,
            moduleProgressService);
        var (retakeOrResultsLabel, retakeOrResultsUrl) = ModuleProgressDisplay.BuildRetakeOrResultsLink(module, assessment);

        return View(new ModuleOverviewViewModel
        {
            Name = module.Name,
            ModulePosition = module.Position,
            Title = module.Title,
            Description = module.Description,
            Outcomes = markdownRenderer.Render(module.Outcomes),
            Criteria = markdownRenderer.Render(module.Criteria),
            Duration = module.Duration,
            ProgressPercentage = percentage,
            ProgressSummary = ModuleProgressDisplay.BuildProgressSummary(percentage, assessment),
            ProgressDescription = ModuleOverviewDisplay.BuildProgressDescription(module, progress, percentage),
            IsCompleted = progress?.CompletedAt is not null,
            ActionLabel = actionLabel,
            ActionUrl = actionUrl,
            RetakeOrResultsLabel = retakeOrResultsLabel,
            RetakeOrResultsUrl = retakeOrResultsUrl,
            Sections = ModuleOverviewDisplay.BuildSections(module, progress, assessment, moduleProgressService),
        });
    }
}
