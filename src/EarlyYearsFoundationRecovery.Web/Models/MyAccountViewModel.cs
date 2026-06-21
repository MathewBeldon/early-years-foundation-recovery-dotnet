namespace EarlyYearsFoundationRecovery.Web.Models;

public class MyAccountViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string AuthorityName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string ExperienceName { get; set; } = string.Empty;
    public string TrainingEmailsPreference { get; set; } = string.Empty;
    public string ResearchPreference { get; set; } = string.Empty;
    public string? Notice { get; set; }
    public bool ShowFeedbackCta { get; set; }
}
