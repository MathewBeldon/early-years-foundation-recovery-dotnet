using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

public static class ModuleProgressDisplay
{
    public static int CalculatePercentage(UserModuleProgress? progress, TrainingModuleContent module)
    {
        if (progress is null || module.ContentPageCount == 0)
        {
            return 0;
        }

        var visited = module.ContentPages.Count(p => progress.VisitedPages.ContainsKey(p.Name));

        if (progress.CompletedAt is not null && visited < module.ContentPageCount)
        {
            return 100;
        }

        return (int)Math.Round(visited * 100.0 / module.ContentPageCount);
    }

    public static string BuildProgressSummary(int percentage, Assessment? assessment)
    {
        if (AssessmentProgressService.IsFailed(assessment) && assessment?.Score is float score)
        {
            return $"{percentage}% of pages viewed — end of module test score {score:0}% (pass mark is {QuestionAnswerService.PassThreshold:0}%)";
        }

        return $"{percentage}% complete";
    }

    public static (string? Label, string? Url) BuildRetakeOrResultsLink(
        TrainingModuleContent module,
        Assessment? assessment)
    {
        if (!AssessmentProgressService.IsGraded(assessment))
        {
            return (null, null);
        }

        if (AssessmentProgressService.IsFailed(assessment) && module.AssessmentIntroPage is not null)
        {
            return (
                "Retake end of module test",
                TrainingModuleContent.ContentUrl(module.Name, module.AssessmentIntroPage));
        }

        if (module.AssessmentResultsPage is not null)
        {
            return (
                "View previous test result",
                TrainingModuleContent.ContentUrl(module.Name, module.AssessmentResultsPage));
        }

        return (null, null);
    }

    public static (string Label, string Url) BuildPrimaryAction(
        TrainingModuleContent module,
        UserModuleProgress? progress,
        Assessment? assessment,
        ModuleProgressService moduleProgressService)
    {
        var isCompleted = progress?.CompletedAt is not null;
        var hasStarted = progress?.StartedAt is not null;
        var failedAttempt = AssessmentProgressService.IsFailed(assessment);
        var resumePage = moduleProgressService.ResolveResumePage(progress, module);

        if (isCompleted && module.CertificatePage is not null)
        {
            return ("View certificate", TrainingModuleContent.ContentUrl(module.Name, module.CertificatePage));
        }

        if (failedAttempt && module.AssessmentIntroPage is not null)
        {
            return ("Retake test", TrainingModuleContent.ContentUrl(module.Name, module.AssessmentIntroPage));
        }

        if (hasStarted)
        {
            var url = resumePage is null
                ? $"/modules/{module.Name}"
                : TrainingModuleContent.ContentUrl(module.Name, resumePage);
            return ("Resume module", url);
        }

        var startUrl = resumePage is null
            ? $"/modules/{module.Name}"
            : TrainingModuleContent.ContentUrl(module.Name, resumePage);
        return ("Start module", startUrl);
    }
}
