namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class User : ITimestamped
{
    public long Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Country { get; set; }
    public string? GovOneId { get; set; }
    public bool RegistrationComplete { get; set; }
    public bool? TrainingEmails { get; set; }
    public string? SettingType { get; set; }
    public string? SettingTypeOther { get; set; }
    public string? LocalAuthority { get; set; }
    public string? RoleType { get; set; }
    public string? RoleTypeOther { get; set; }
    public string? EarlyYearsExperience { get; set; }
    public bool? ResearchParticipant { get; set; }
    public DateTime? TermsAndConditionsAgreedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedReason { get; set; }
    public string? ClosedReasonCustom { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserModuleProgress> ModuleProgress { get; set; } = [];
    public ICollection<Assessment> Assessments { get; set; } = [];
    public ICollection<Response> Responses { get; set; } = [];
    public ICollection<Note> Notes { get; set; } = [];
    public ICollection<MailEvent> MailEvents { get; set; } = [];
}
