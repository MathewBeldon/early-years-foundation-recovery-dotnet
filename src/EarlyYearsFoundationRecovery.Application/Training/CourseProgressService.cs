using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed record CourseProgressSnapshot(
    IReadOnlyList<ModuleCardData> InProgressModules,
    IReadOnlyList<ModuleCardData> AvailableModules,
    IReadOnlyList<UpcomingModuleData> UpcomingModules,
    IReadOnlyList<CompletedModuleData> CompletedModules,
    bool CourseCompleted,
    bool CompletedAllModules,
    bool HasCompletedModules,
    bool ShowAvailableSection,
    bool ShowInProgressEmptyState,
    bool ShowChooseAvailableModuleEmptyState);

public sealed record ModuleCardData(
    TrainingModuleContent Module,
    UserModuleProgress? Progress,
    Assessment? Assessment);

public sealed record UpcomingModuleData(
    string Name,
    string Title,
    string UpcomingText,
    string AboutUrl);

public sealed record CompletedModuleData(
    string Name,
    string Title,
    string ModuleUrl,
    string CompletedAt,
    string CertificateUrl);

public static class CourseProgressService
{
    public static CourseProgressSnapshot Build(
        IReadOnlyList<TrainingModuleContent> allModules,
        IReadOnlyDictionary<string, UserModuleProgress> progressByModule,
        IReadOnlyDictionary<string, Assessment> assessmentsByModule)
    {
        var orderedModules = allModules.OrderBy(m => m.Position).ToList();
        var liveModules = orderedModules.Where(m => m.Live).ToList();

        var inProgress = new List<ModuleCardData>();
        var available = new List<ModuleCardData>();
        var completed = new List<CompletedModuleData>();

        foreach (var module in liveModules)
        {
            progressByModule.TryGetValue(module.Name, out var progress);
            assessmentsByModule.TryGetValue(module.Name, out var assessment);

            if (progress?.CompletedAt is not null)
            {
                completed.Add(new CompletedModuleData(
                    module.Name,
                    module.Title,
                    $"/modules/{module.Name}",
                    progress.CompletedAt.Value.ToString("d MMMM yyyy"),
                    module.CertificatePage is null
                        ? $"/modules/{module.Name}"
                        : TrainingModuleContent.ContentUrl(module.Name, module.CertificatePage)));
            }
            else if (progress?.StartedAt is not null)
            {
                inProgress.Add(new ModuleCardData(module, progress, assessment));
            }
            else
            {
                available.Add(new ModuleCardData(module, progress, assessment));
            }
        }

        var upcoming = orderedModules
            .Where(m => !m.Live)
            .Select(m => new UpcomingModuleData(
                m.Name,
                m.Title,
                string.IsNullOrWhiteSpace(m.Upcoming)
                    ? "This module will be available soon."
                    : m.Upcoming,
                $"/about/{m.Name}"))
            .ToList();

        var courseCompleted = liveModules.Count > 0 &&
                              liveModules.All(m => progressByModule.TryGetValue(m.Name, out var progress) &&
                                                   progress?.CompletedAt is not null);

        var completedAllModules = courseCompleted &&
                                  inProgress.Count == 0 &&
                                  available.Count == 0 &&
                                  upcoming.Count == 0;

        return new CourseProgressSnapshot(
            inProgress,
            available,
            upcoming,
            completed,
            courseCompleted,
            completedAllModules,
            completed.Count > 0,
            available.Count > 0 || courseCompleted,
            inProgress.Count == 0 && completed.Count == 0,
            inProgress.Count == 0 && completed.Count > 0);
    }
}
