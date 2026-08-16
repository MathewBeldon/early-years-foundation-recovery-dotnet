using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

/// <summary>
/// Certificate eligibility from Rails v1.5.0 commit ac546721. Failed assessment
/// results offer My modules and Retake test; only a passed attempt continues.
/// </summary>
public static class ModuleCompletionPolicy
{
    public static bool RequiresPassedAssessment(TrainingModuleContent module) =>
        module.Pages.Any(page => page.IsSummative);

    public static bool CanAccessCertificate(TrainingModuleContent module, Assessment? assessment) =>
        !RequiresPassedAssessment(module) || AssessmentProgressService.IsPassed(assessment);

    public static string BlockedCertificateDestination(TrainingModuleContent module) =>
        module.AssessmentIntroPage is null
            ? $"/modules/{module.Name}"
            : TrainingModuleContent.ContentUrl(module.Name, module.AssessmentIntroPage);
}
