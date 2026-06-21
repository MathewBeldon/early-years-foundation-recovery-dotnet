using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed record SectionBarDisplay(
    string? SectionNumbers,
    string SectionHeading,
    string PageNumbers,
    int Percentage);

public static class SectionBarBuilder
{
    public static SectionBarDisplay? Build(TrainingModuleContent module, TrainingPageContent currentPage)
    {
        var visibleSections = module.ContentSections
            .Where(section => !ShouldHideSection(section))
            .ToList();

        for (var sectionIndex = 0; sectionIndex < visibleSections.Count; sectionIndex++)
        {
            var sectionPages = visibleSections[sectionIndex];
            var pageIndex = sectionPages.ToList().FindIndex(page =>
                string.Equals(page.Name, currentPage.Name, StringComparison.OrdinalIgnoreCase));

            if (pageIndex < 0)
            {
                continue;
            }

            var sectionNumbers = visibleSections.Count > 1
                ? $"Section {sectionIndex + 1} of {visibleSections.Count}"
                : null;

            return new SectionBarDisplay(
                SectionNumbers: sectionNumbers,
                SectionHeading: ResolveSectionHeading(sectionPages, currentPage),
                PageNumbers: $"Page {pageIndex + 1} of {sectionPages.Count}",
                Percentage: (int)Math.Round((pageIndex + 1) * 100.0 / sectionPages.Count));
        }

        return null;
    }

    private static bool ShouldHideSection(IReadOnlyList<TrainingPageContent> sectionPages)
    {
        var first = sectionPages.FirstOrDefault();
        return first is not null && first.PageType is "pre_confidence" or "pre_confidence_intro" or "feedback_question";
    }

    private static string ResolveSectionHeading(
        IReadOnlyList<TrainingPageContent> sectionPages,
        TrainingPageContent currentPage)
    {
        if (string.Equals(currentPage.PageType, "certificate", StringComparison.OrdinalIgnoreCase))
        {
            return "Summary and next steps";
        }

        return sectionPages[0].Heading;
    }
}
