namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface ITrainingContentProvider
{
    Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default);
    Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default);
    Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default);
}

public sealed record TrainingModuleContent(
    string Name,
    string Title,
    string Description,
    string Outcomes,
    string Criteria,
    decimal Duration,
    int Position,
    bool Live,
    IReadOnlyList<TrainingPageContent> Pages,
    string? Upcoming = null)
{
    public IEnumerable<TrainingPageContent> ContentPages =>
        Pages.Where(p => p.PageType is not "interruption_page");

    public IReadOnlyList<TrainingPageContent> ModuleContent => ContentPages.ToList();

    public int ContentPageCount => ModuleContent.Count;

    public int SubmoduleCount => ContentSections.Count(section => !ShouldHideSection(section));

    public IReadOnlyList<IReadOnlyList<TrainingPageContent>> ContentSections =>
        SliceBefore(ModuleContent, page => page.IsSection);

    public IReadOnlyList<IReadOnlyList<TrainingPageContent>> ContentSubsections(IReadOnlyList<TrainingPageContent> sectionPages) =>
        SliceBefore(sectionPages, page => page.IsSubsection);

    public IReadOnlyList<TrainingPageContent> SummativeQuestions =>
        Pages.Where(p => p.IsSummative).ToList();

    public TrainingPageContent? PageByName(string pageName) =>
        Pages.FirstOrDefault(p => string.Equals(p.Name, pageName, StringComparison.OrdinalIgnoreCase));

    public TrainingPageContent? FirstPage => Pages.FirstOrDefault();

    public TrainingPageContent? FirstContentPage =>
        Pages.FirstOrDefault(p => p.PageType is "topic_intro" or "text_page") ?? ContentPages.FirstOrDefault();

    public TrainingPageContent? CertificatePage =>
        Pages.FirstOrDefault(p => p.PageType == "certificate");

    public TrainingPageContent? AssessmentIntroPage =>
        Pages.FirstOrDefault(p => p.PageType == "assessment_intro");

    public TrainingPageContent? AssessmentResultsPage =>
        Pages.FirstOrDefault(p => p.PageType == "assessment_results");

    public TrainingPageContent? NextPageAfter(string pageName)
    {
        var index = Pages.ToList().FindIndex(p => string.Equals(p.Name, pageName, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index < Pages.Count - 1 ? Pages[index + 1] : null;
    }

    public bool IsLastSummativeQuestion(string questionName)
    {
        var summative = SummativeQuestions;
        return summative.Count > 0 &&
               string.Equals(summative[^1].Name, questionName, StringComparison.OrdinalIgnoreCase);
    }

    public string TabLabel => $"Module {Position}";

    public string TabAnchor => TabLabel.ToLowerInvariant().Replace(' ', '-');

    public static string ContentUrl(string moduleName, TrainingPageContent content)
    {
        if (content.IsQuestion)
        {
            return $"/modules/{moduleName}/questionnaires/{content.Name}";
        }

        if (content.PageType == "assessment_results")
        {
            return $"/modules/{moduleName}/assessment-result/{content.Name}";
        }

        return $"/modules/{moduleName}/content-pages/{content.Name}";
    }

    private static bool ShouldHideSection(IReadOnlyList<TrainingPageContent> sectionPages)
    {
        var first = sectionPages.FirstOrDefault();
        return first is not null && (
            first.PageType is "pre_confidence" or "pre_confidence_intro" or "feedback_question");
    }

    private static IReadOnlyList<IReadOnlyList<TrainingPageContent>> SliceBefore(
        IReadOnlyList<TrainingPageContent> pages,
        Func<TrainingPageContent, bool> startsNewGroup)
    {
        if (pages.Count == 0)
        {
            return [];
        }

        var groups = new List<IReadOnlyList<TrainingPageContent>>();
        var current = new List<TrainingPageContent>();

        foreach (var page in pages)
        {
            if (startsNewGroup(page) && current.Count > 0)
            {
                groups.Add(current);
                current = [];
            }

            current.Add(page);
        }

        if (current.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }
}

public sealed record TrainingPageContent(
    string Name,
    string PageType,
    string Heading,
    string Body,
    IReadOnlyList<QuestionAnswerOption> Answers,
    string? SuccessMessage,
    string? FailureMessage,
    bool Notes = false)
{
    public bool IsQuestion => PageType is "formative" or "summative";
    public bool IsFormative => PageType == "formative";
    public bool IsSummative => PageType == "summative";
    public bool SupportsNotes => (PageType is "topic_intro" or "text_page") && Notes;
    public bool IsSection => PageType is "submodule_intro" or "summary_intro" or "feedback_intro" or "certificate";
    public bool IsSubsection => PageType is "topic_intro" or "recap_page" or "assessment_intro" or "confidence_intro" or "certificate";
    public bool IsCertificate => PageType == "certificate";
    public bool IsAssessmentIntro => PageType == "assessment_intro";

    public static TrainingPageContent CreatePage(string name, string pageType, string heading, string body) =>
        new(name, pageType, heading, body, [], null, null);
}

public sealed record QuestionAnswerOption(string Text, bool Correct);
