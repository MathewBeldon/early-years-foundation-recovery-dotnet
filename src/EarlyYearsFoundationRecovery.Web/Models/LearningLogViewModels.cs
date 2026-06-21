namespace EarlyYearsFoundationRecovery.Web.Models;

public class LearningLogViewModel
{
    public IReadOnlyList<LearningLogModuleTabViewModel> Modules { get; set; } = [];
}

public class LearningLogModuleTabViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TabLabel { get; set; } = string.Empty;
    public string TabAnchor { get; set; } = string.Empty;
    public IReadOnlyList<LearningLogNoteViewModel> Notes { get; set; } = [];
}

public class LearningLogNoteViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string LoggedAt { get; set; } = string.Empty;
    public string PageUrl { get; set; } = string.Empty;
    public bool Filled { get; set; }
}

public class LearningLogNoteFormViewModel
{
    public string? Body { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TrainingModule { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NextPageName { get; set; }
    public string? NextPageModule { get; set; }
    public string? NextPageUrl { get; set; }
    public string PageType { get; set; } = string.Empty;
    public string? PreviousPageUrl { get; set; }
    public string PreviousPageLabel { get; set; } = "Previous";
    public string SubmitLabel { get; set; } = "Save and continue";
    public string LearningLogAnchor { get; set; } = string.Empty;
}
