namespace EarlyYearsFoundationRecovery.Web.Models;

public class FeedbackIndexViewModel
{
    public bool IsComplete { get; set; }
    public string? FirstQuestionUrl { get; set; }
}

public class FeedbackQuestionViewModel
{
    public string QuestionName { get; set; } = string.Empty;
    public string Heading { get; set; } = string.Empty;
    public string Legend { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string InputType { get; set; } = string.Empty;
    public IReadOnlyList<string> Options { get; set; } = [];
    public bool Skippable { get; set; }
    public bool HasOther { get; set; }
    public string? OtherLabel { get; set; }
    public bool HasMore { get; set; }
    public bool HasOr { get; set; }
    public string? OrLabel { get; set; }
    public IReadOnlyList<string> SelectedAnswers { get; set; } = [];
    public string? TextInput { get; set; }
    public string? PreviousUrl { get; set; }
    public string SubmitLabel { get; set; } = "Next";
    public bool ShowPrevious { get; set; }
    public bool IsProfileUpdate { get; set; }
    public int OtherOptionIndex { get; set; }
    public int OrOptionIndex { get; set; }
}

public class FeedbackThankYouViewModel
{
    public string Heading { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class FeedbackSubmitModel
{
    public List<string> SelectedAnswers { get; set; } = [];
    public string? TextInput { get; set; }
    public string? From { get; set; }
}
