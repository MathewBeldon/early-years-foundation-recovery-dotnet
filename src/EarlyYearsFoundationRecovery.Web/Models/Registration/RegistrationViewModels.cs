using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Web.Models.Registration;

public class TermsAndConditionsViewModel
{
    public bool Accepted { get; set; }
}

public class NameViewModel
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class WhereYouLiveViewModel
{
    public string CountryId { get; set; } = string.Empty;
    public IReadOnlyList<ReferenceOption> Options { get; set; } = [];
}

public class SettingTypeViewModel
{
    public string SettingTypeId { get; set; } = string.Empty;
    public IReadOnlyList<SettingTypeOption> Options { get; set; } = [];
}

public class SettingTypeOtherViewModel
{
    public string SettingTypeOther { get; set; } = string.Empty;
}

public class LocalAuthorityViewModel
{
    public string? LocalAuthorityId { get; set; }
    public bool Skip { get; set; }
    public IReadOnlyList<ReferenceOption> Options { get; set; } = [];
}

public class RoleTypeViewModel
{
    public string RoleTypeId { get; set; } = string.Empty;
    public IReadOnlyList<RoleOption> Options { get; set; } = [];
}

public class RoleTypeOtherViewModel
{
    public string RoleTypeOther { get; set; } = string.Empty;
}

public class EarlyYearsExperienceViewModel
{
    public string ExperienceId { get; set; } = string.Empty;
    public IReadOnlyList<ReferenceOption> Options { get; set; } = [];
}

public class TrainingEmailsViewModel
{
    public bool? TrainingEmails { get; set; }
}

public class ResearchParticipantViewModel
{
    public bool? ResearchParticipant { get; set; }
}

public class CheckYourAnswersViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    public string SettingName { get; set; } = string.Empty;
    public string AuthorityName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string ExperienceName { get; set; } = string.Empty;
    public bool ShowExperience { get; set; }
    public bool ShowAuthority { get; set; }
    public bool ShowRole { get; set; }
    public string TrainingEmailsPreference { get; set; } = string.Empty;
    public string ResearchPreference { get; set; } = string.Empty;
}
