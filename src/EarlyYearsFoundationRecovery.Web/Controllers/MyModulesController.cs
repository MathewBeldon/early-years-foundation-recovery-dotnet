using EarlyYearsFoundationRecovery.Application.Feedback;
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
public class MyModulesController(
    IUserRepository users,
    ITrainingContentProvider contentProvider,
    IUserModuleProgressRepository progressRepository,
    ITrainingAssessmentRepository assessmentRepository,
    ModuleProgressService moduleProgressService,
    CourseFeedbackService feedbackService) : Controller
{
    [HttpGet("/my-modules")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId()
            ?? throw new InvalidOperationException("User is not authenticated.");

        var user = await users.GetByIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var allModules = await contentProvider.GetAllModulesAsync(cancellationToken);
        var progressRecords = await progressRepository.GetForUserAsync(userId, cancellationToken);
        var progressByModule = progressRecords.ToDictionary(p => p.ModuleName, StringComparer.OrdinalIgnoreCase);

        var startedModuleNames = progressRecords
            .Where(p => p.StartedAt is not null)
            .Select(p => p.ModuleName);
        var assessmentsByModule = await assessmentRepository.GetLatestAssessmentsByModuleAsync(
            userId,
            startedModuleNames,
            cancellationToken);

        var snapshot = CourseProgressService.Build(allModules, progressByModule, assessmentsByModule);

        var displayName = $"{user.FirstName} {user.LastName}".Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = user.Email;
        }

        return View(new MyModulesViewModel
        {
            DisplayName = displayName,
            CompletedAllModules = snapshot.CompletedAllModules,
            ShowInProgressEmptyState = snapshot.ShowInProgressEmptyState,
            ShowChooseAvailableModuleEmptyState = snapshot.ShowChooseAvailableModuleEmptyState,
            ShowAvailableSection = snapshot.ShowAvailableSection,
            ShowCourseCompletedMessage = snapshot.CourseCompleted && snapshot.AvailableModules.Count == 0,
            ShowFeedbackCta = !await feedbackService.IsCompleteAsync(userId, cancellationToken),
            InProgressModules = snapshot.InProgressModules
                .Select(item => BuildCard(item.Module, item.Progress, item.Assessment))
                .ToList(),
            AvailableModules = snapshot.AvailableModules
                .Select(item => BuildCard(item.Module, item.Progress, item.Assessment))
                .ToList(),
            UpcomingModules = snapshot.UpcomingModules
                .Select(item => new UpcomingModuleCardViewModel
                {
                    Name = item.Name,
                    Title = item.Title,
                    UpcomingText = item.UpcomingText,
                    AboutUrl = item.AboutUrl,
                })
                .ToList(),
            CompletedModules = snapshot.CompletedModules
                .Select(item => new CompletedModuleRowViewModel
                {
                    Title = item.Title,
                    ModuleUrl = item.ModuleUrl,
                    CompletedAt = item.CompletedAt,
                    CertificateUrl = item.CertificateUrl,
                })
                .ToList(),
        });
    }

    private ModuleCardViewModel BuildCard(
        TrainingModuleContent module,
        UserModuleProgress? progress,
        Assessment? assessment)
    {
        var percentage = moduleProgressService.CalculatePercentage(progress, module);
        var hasStarted = progress?.StartedAt is not null;
        var (actionLabel, actionUrl) = ModuleProgressDisplay.BuildPrimaryAction(
            module,
            progress,
            assessment,
            moduleProgressService);
        var (retakeOrResultsLabel, retakeOrResultsUrl) = hasStarted
            ? ModuleProgressDisplay.BuildRetakeOrResultsLink(module, assessment)
            : (null, null);

        return new ModuleCardViewModel
        {
            Name = module.Name,
            Title = module.Title,
            Description = module.Description,
            Duration = module.Duration,
            ProgressPercentage = percentage,
            ProgressSummary = ModuleProgressDisplay.BuildProgressSummary(percentage, assessment),
            ProgressDescription = hasStarted
                ? ModuleOverviewDisplay.BuildProgressDescription(module, progress, percentage)
                : string.Empty,
            IsCompleted = progress?.CompletedAt is not null,
            ShowProgress = hasStarted,
            ActionLabel = actionLabel,
            ActionUrl = actionUrl,
            RetakeOrResultsLabel = retakeOrResultsLabel,
            RetakeOrResultsUrl = retakeOrResultsUrl,
            ThumbnailUrl = ModuleThumbnailUrls.ForModule(module.Name),
        };
    }
}
