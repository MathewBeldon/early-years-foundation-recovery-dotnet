using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

public enum ModuleSectionStatus
{
    NotStarted,
    Started,
    Completed,
    Failed,
}

public sealed record ModuleOverviewSubsection(
    string Heading,
    string? PageUrl,
    ModuleSectionStatus Status,
    string StatusLabel);

public sealed record ModuleOverviewSection(
    string Heading,
    string? PageCountLabel,
    int? Position,
    bool ShowConnectorLine,
    ModuleSectionStatus Status,
    IReadOnlyList<ModuleOverviewSubsection> Subsections,
    bool Hide);

public static class ModuleOverviewDisplay
{
    public static IReadOnlyList<ModuleOverviewSection> BuildSections(
        TrainingModuleContent module,
        UserModuleProgress? progress,
        Assessment? assessment,
        ModuleProgressService moduleProgressService)
    {
        var isCompleted = progress?.CompletedAt is not null;
        var failedAttempt = AssessmentProgressService.IsFailed(assessment);
        var passedAttempt = AssessmentProgressService.IsPassed(assessment);
        var resumePage = moduleProgressService.ResolveResumePage(progress, module);
        var visiblePosition = 1;
        var sections = new List<ModuleOverviewSection>();

        foreach (var sectionPages in module.ContentSections)
        {
            var hide = ShouldHideSection(sectionPages);
            var position = hide ? (int?)null : visiblePosition;
            if (!hide)
            {
                visiblePosition++;
            }

            var firstItem = sectionPages[0];
            var sectionStatus = ResolveGroupStatus(sectionPages, progress, isCompleted);
            var showLine = position is not null && position < module.SubmoduleCount;

            sections.Add(new ModuleOverviewSection(
                Heading: ResolveSectionHeading(firstItem),
                PageCountLabel: BuildPageCountLabel(sectionPages),
                Position: position,
                ShowConnectorLine: showLine,
                Status: sectionStatus,
                Subsections: BuildSubsections(
                    module,
                    sectionPages,
                    progress,
                    isCompleted,
                    failedAttempt,
                    passedAttempt,
                    resumePage),
                Hide: hide));
        }

        return sections.Where(section => !section.Hide).ToList();
    }

    public static string BuildProgressDescription(
        TrainingModuleContent module,
        UserModuleProgress? progress,
        int percentage)
    {
        if (progress is null)
        {
            return string.Empty;
        }

        var visited = module.ContentPages.Count(p => progress.VisitedPages.ContainsKey(p.Name));

        return $"You have read {visited} pages";
    }

    private static IReadOnlyList<ModuleOverviewSubsection> BuildSubsections(
        TrainingModuleContent module,
        IReadOnlyList<TrainingPageContent> sectionPages,
        UserModuleProgress? progress,
        bool isCompleted,
        bool failedAttempt,
        bool passedAttempt,
        TrainingPageContent? resumePage)
    {
        var subsections = new List<ModuleOverviewSubsection>();

        foreach (var subsectionPages in module.ContentSubsections(sectionPages))
        {
            var subsectionItem = subsectionPages[0];
            var status = ResolveGroupStatus(subsectionPages, progress, isCompleted);

            if (failedAttempt && subsectionItem.IsAssessmentIntro)
            {
                status = ModuleSectionStatus.Failed;
            }
            else if (passedAttempt && subsectionItem.IsAssessmentIntro)
            {
                status = ModuleSectionStatus.Completed;
            }

            string? pageUrl = null;
            if (IsSubsectionClickable(sectionPages, subsectionPages, progress, isCompleted))
            {
                var targetPage = status == ModuleSectionStatus.Started ? resumePage : subsectionItem;
                pageUrl = targetPage is null
                    ? null
                    : TrainingModuleContent.ContentUrl(module.Name, targetPage);
            }

            subsections.Add(new ModuleOverviewSubsection(
                Heading: subsectionItem.Heading,
                PageUrl: pageUrl,
                Status: status,
                StatusLabel: StatusLabel(status)));
        }

        return subsections;
    }

    private static bool IsSubsectionClickable(
        IReadOnlyList<TrainingPageContent> sectionPages,
        IReadOnlyList<TrainingPageContent> subsectionPages,
        UserModuleProgress? progress,
        bool isCompleted)
    {
        if (isCompleted)
        {
            return true;
        }

        if (!IsVisited(progress, sectionPages[0].Name))
        {
            return false;
        }

        return AllVisited(subsectionPages, progress) || AnyVisited(subsectionPages, progress);
    }

    private static bool ShouldHideSection(IReadOnlyList<TrainingPageContent> sectionPages)
    {
        var first = sectionPages.FirstOrDefault();
        return first is not null && first.PageType is
            "pre_confidence" or "pre_confidence_intro" or "feedback_question";
    }

    private static string ResolveSectionHeading(TrainingPageContent page) =>
        page.IsCertificate ? "Complete module" : page.Heading;

    private static string? BuildPageCountLabel(IReadOnlyList<TrainingPageContent> pages) =>
        pages.Count == 1 ? null : $"({pages.Count} pages)";

    private static ModuleSectionStatus ResolveGroupStatus(
        IReadOnlyList<TrainingPageContent> pages,
        UserModuleProgress? progress,
        bool isCompleted)
    {
        if (isCompleted || AllVisited(pages, progress))
        {
            return ModuleSectionStatus.Completed;
        }

        if (AnyVisited(pages, progress))
        {
            return ModuleSectionStatus.Started;
        }

        return ModuleSectionStatus.NotStarted;
    }

    private static bool AllVisited(IReadOnlyList<TrainingPageContent> pages, UserModuleProgress? progress) =>
        pages.All(page => IsVisited(progress, page.Name));

    private static bool AnyVisited(IReadOnlyList<TrainingPageContent> pages, UserModuleProgress? progress) =>
        pages.Any(page => IsVisited(progress, page.Name));

    private static bool IsVisited(UserModuleProgress? progress, string pageName) =>
        progress?.VisitedPages.ContainsKey(pageName) == true;

    private static string StatusLabel(ModuleSectionStatus status) => status switch
    {
        ModuleSectionStatus.Completed => "completed",
        ModuleSectionStatus.Started => "in progress",
        ModuleSectionStatus.Failed => "retake test",
        _ => "not started",
    };
}
