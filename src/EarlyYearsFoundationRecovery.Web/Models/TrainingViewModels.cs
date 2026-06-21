using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.Web.Models;

public class MyModulesViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public bool CompletedAllModules { get; set; }
    public bool ShowInProgressEmptyState { get; set; }
    public bool ShowChooseAvailableModuleEmptyState { get; set; }
    public bool ShowAvailableSection { get; set; }
    public bool ShowCourseCompletedMessage { get; set; }
    public bool ShowFeedbackCta { get; set; }
    public IReadOnlyList<ModuleCardViewModel> InProgressModules { get; set; } = [];
    public IReadOnlyList<ModuleCardViewModel> AvailableModules { get; set; } = [];
    public IReadOnlyList<UpcomingModuleCardViewModel> UpcomingModules { get; set; } = [];
    public IReadOnlyList<CompletedModuleRowViewModel> CompletedModules { get; set; } = [];
}

public class UpcomingModuleCardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string UpcomingText { get; set; } = string.Empty;
    public string AboutUrl { get; set; } = string.Empty;
}

public class CompletedModuleRowViewModel
{
    public string Title { get; set; } = string.Empty;
    public string ModuleUrl { get; set; } = string.Empty;
    public string CompletedAt { get; set; } = string.Empty;
    public string CertificateUrl { get; set; } = string.Empty;
}

public class ModuleCardViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Duration { get; set; }
    public int ProgressPercentage { get; set; }
    public string ProgressSummary { get; set; } = string.Empty;
    public string ProgressDescription { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool ShowProgress { get; set; }
    public string ActionLabel { get; set; } = "View module";
    public string ActionUrl { get; set; } = string.Empty;
    public string? RetakeOrResultsLabel { get; set; }
    public string? RetakeOrResultsUrl { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
}

public class ModuleOverviewViewModel
{
    public string Name { get; set; } = string.Empty;
    public int ModulePosition { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Outcomes { get; set; } = string.Empty;
    public string Criteria { get; set; } = string.Empty;
    public decimal Duration { get; set; }
    public int ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }
    public string ActionLabel { get; set; } = "Start module";
    public string ActionUrl { get; set; } = string.Empty;
    public string ProgressSummary { get; set; } = string.Empty;
    public string? RetakeOrResultsLabel { get; set; }
    public string? RetakeOrResultsUrl { get; set; }
    public string ProgressDescription { get; set; } = string.Empty;
    public IReadOnlyList<ModuleOverviewSection> Sections { get; set; } = [];
}

public class TrainingPageViewModel
{
    public string ModuleName { get; set; } = string.Empty;
    public int ModulePosition { get; set; }
    public string ModuleTitle { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string? NextPageUrl { get; set; }
    public string NextPageLabel { get; set; } = "Continue";
    public string? PreviousPageUrl { get; set; }
    public string PreviousPageLabel { get; set; } = "Previous";
    public string? BackUrl { get; set; }
    public string BackLinkText { get; set; } = string.Empty;
    public string ContinueLabel { get; set; } = "Continue";
    public float? AssessmentScore { get; set; }
    public bool? AssessmentPassed { get; set; }
    public bool IsCompleted { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string Criteria { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public string? CertificateDownloadUrl { get; set; }
    public bool SupportsNotes { get; set; }
    public string ProgressSummary { get; set; } = string.Empty;
    public string? RetakeOrResultsLabel { get; set; }
    public string? RetakeOrResultsUrl { get; set; }
    public LearningLogNoteFormViewModel? NoteForm { get; set; }
    public SectionBarDisplay? SectionBar { get; set; }
}

public class TrainingQuestionViewModel
{
    public string ModuleName { get; set; } = string.Empty;
    public int ModulePosition { get; set; }
    public string ModuleTitle { get; set; } = string.Empty;
    public string QuestionName { get; set; } = string.Empty;
    public string PageType { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public IReadOnlyList<QuestionAnswerOptionViewModel> Answers { get; set; } = [];
    public string? SelectedAnswer { get; set; }
    public bool ShowFeedback { get; set; }
    public bool? IsCorrect { get; set; }
    public string? FeedbackMessage { get; set; }
    public string? NextPageUrl { get; set; }
    public string NextPageLabel { get; set; } = "Continue";
    public string? PreviousPageUrl { get; set; }
    public string PreviousPageLabel { get; set; } = "Previous";
    public string BackUrl { get; set; } = string.Empty;
    public string BackLinkText { get; set; } = string.Empty;
    public bool CanSubmit { get; set; } = true;
    public bool IsFormative { get; set; }
    public string SubmitLabel { get; set; } = "Next";
    public string? BannerTitle { get; set; }
    public string? BannerCssClass { get; set; }
    public SectionBarDisplay? SectionBar { get; set; }
}

public class QuestionAnswerOptionViewModel
{
    public string Text { get; set; } = string.Empty;
    public bool Correct { get; set; }
    public bool Checked { get; set; }
    public bool Disabled { get; set; }
    public string? StatusHint { get; set; }
    public bool EmphasiseLabel { get; set; }
}
