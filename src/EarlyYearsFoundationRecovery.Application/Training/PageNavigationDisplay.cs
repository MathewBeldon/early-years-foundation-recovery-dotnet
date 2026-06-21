using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Application.Training;

public static class PageNavigationDisplay
{
    public static string BuildBackLinkText(TrainingModuleContent module) =>
        $"Back to Module {module.Position} overview";

    public static (string? Url, string Label) BuildPrevious(TrainingModuleContent module, TrainingPageContent page)
    {
        if (page.PageType == "interruption_page")
        {
            return ($"/modules/{module.Name}", "Previous");
        }

        var pages = module.Pages.ToList();
        var index = pages.FindIndex(p => string.Equals(p.Name, page.Name, StringComparison.OrdinalIgnoreCase));
        if (index <= 0)
        {
            return (null, "Previous");
        }

        var previous = pages[index - 1];
        return (TrainingModuleContent.ContentUrl(module.Name, previous), "Previous");
    }

    public static (string Url, string Label) BuildNext(
        TrainingModuleContent module,
        TrainingPageContent page,
        TrainingPageContent? nextPage)
    {
        if (nextPage is null)
        {
            return ("/my-modules", "Continue");
        }

        var url = TrainingModuleContent.ContentUrl(module.Name, nextPage);
        var label = ResolveNextLabel(page, nextPage);
        return (url, label);
    }

    private static string ResolveNextLabel(TrainingPageContent page, TrainingPageContent nextPage)
    {
        if (page.PageType == "interruption_page")
        {
            return "Continue";
        }

        if (page.IsSection && !page.PageType.Contains("feedback", StringComparison.OrdinalIgnoreCase))
        {
            return "Start section";
        }

        if (nextPage.IsSummative && !page.IsSummative && page.PageType != "summative")
        {
            return "Start test";
        }

        if (nextPage.PageType == "assessment_results" && page.IsSummative)
        {
            return "Finish test";
        }

        if (nextPage.PageType == "certificate")
        {
            return "View certificate";
        }

        if (page.SupportsNotes || page.IsSummative)
        {
            return "Save and continue";
        }

        return "Continue";
    }
}
