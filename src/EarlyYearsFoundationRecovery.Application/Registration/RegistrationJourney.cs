using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Registration;

public static class RegistrationJourney
{
    public const string NotApplicable = "Not applicable";
    public const string MultipleLocalAuthorities = "Multiple";

    public const string TermsAndConditions = "terms-and-conditions";
    public const string Name = "name";
    public const string WhereYouLive = "where-you-live";
    public const string SettingType = "setting-type";
    public const string SettingTypeOther = "setting-type-other";
    public const string LocalAuthority = "local-authority";
    public const string RoleType = "role-type";
    public const string RoleTypeOther = "role-type-other";
    public const string EarlyYearsExperience = "early-years-experience";
    public const string TrainingEmails = "training-emails";
    public const string ResearchParticipant = "research-participant";
    public const string CheckYourAnswers = "check-your-answers";

    public static string StepPath(string step) => $"/registration/{step}";

    public static bool IsEngland(User user) =>
        string.IsNullOrWhiteSpace(user.Country) ||
        user.Country.Equals("England", StringComparison.OrdinalIgnoreCase);

    public static bool IsNotApplicable(string? value) =>
        string.Equals(value, NotApplicable, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "N/A", StringComparison.OrdinalIgnoreCase);

    public static bool RequiresRoleStep(User user, SettingTypeOption settingType)
    {
        if (settingType.RoleGroup == "none")
        {
            return false;
        }

        if (settingType.Id == "other")
        {
            return !IsEngland(user);
        }

        return true;
    }

    public static bool RequiresExperienceStep(User user, SettingTypeOption settingType)
    {
        if (!RequiresRoleStep(user, settingType))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(user.RoleType) || IsNotApplicable(user.RoleType))
        {
            return false;
        }

        if (user.RoleType == "other" && string.IsNullOrWhiteSpace(user.RoleTypeOther))
        {
            return false;
        }

        return true;
    }

    public static bool LocalAuthorityOutstanding(User user, SettingTypeOption settingType) =>
        IsEngland(user) &&
        settingType.RequiresLocalAuthority &&
        string.IsNullOrWhiteSpace(user.LocalAuthority);

    public static bool RoleOutstanding(User user, SettingTypeOption settingType) =>
        RequiresRoleStep(user, settingType) && string.IsNullOrWhiteSpace(user.RoleType);

    public static bool ExperienceOutstanding(User user, SettingTypeOption settingType) =>
        RequiresExperienceStep(user, settingType) && string.IsNullOrWhiteSpace(user.EarlyYearsExperience);

    public static void ApplySettingTypeReset(User user, SettingTypeOption settingType)
    {
        user.SettingTypeOther = null;
        user.LocalAuthority = settingType.RequiresLocalAuthority ? null : NotApplicable;
        user.RoleType = settingType.RoleGroup == "none" ? NotApplicable : null;
        user.RoleTypeOther = null;
        user.EarlyYearsExperience = null;
    }

    public static string ResolveCurrentStep(User user, IReferenceDataProvider referenceData)
    {
        if (user.TermsAndConditionsAgreedAt is null)
        {
            return TermsAndConditions;
        }

        if (string.IsNullOrWhiteSpace(user.FirstName) || string.IsNullOrWhiteSpace(user.LastName))
        {
            return Name;
        }

        if (string.IsNullOrWhiteSpace(user.Country))
        {
            return WhereYouLive;
        }

        if (string.IsNullOrWhiteSpace(user.SettingType))
        {
            return SettingType;
        }

        var settingType = referenceData.GetSettingType(user.SettingType);
        if (settingType is null)
        {
            return SettingType;
        }

        if (settingType.Id == "other" && string.IsNullOrWhiteSpace(user.SettingTypeOther))
        {
            return SettingTypeOther;
        }

        if (LocalAuthorityOutstanding(user, settingType))
        {
            return LocalAuthority;
        }

        if (RoleOutstanding(user, settingType))
        {
            return RoleType;
        }

        if (user.RoleType == "other" && string.IsNullOrWhiteSpace(user.RoleTypeOther))
        {
            return RoleTypeOther;
        }

        if (ExperienceOutstanding(user, settingType))
        {
            return EarlyYearsExperience;
        }

        if (user.TrainingEmails is null)
        {
            return TrainingEmails;
        }

        if (user.ResearchParticipant is null)
        {
            return ResearchParticipant;
        }

        return CheckYourAnswers;
    }

    public static IReadOnlyList<string> VisibleSteps(User user, IReferenceDataProvider referenceData)
    {
        var steps = new List<string> { TermsAndConditions, Name, WhereYouLive, SettingType };

        var settingType = referenceData.GetSettingType(user.SettingType);
        if (settingType is not null)
        {
            if (settingType.Id == "other")
            {
                steps.Add(SettingTypeOther);
            }

            if (IsEngland(user) && settingType.RequiresLocalAuthority)
            {
                steps.Add(LocalAuthority);
            }

            if (RequiresRoleStep(user, settingType))
            {
                steps.Add(RoleType);
                if (user.RoleType == "other")
                {
                    steps.Add(RoleTypeOther);
                }
            }

            if (RequiresExperienceStep(user, settingType))
            {
                steps.Add(EarlyYearsExperience);
            }
        }

        steps.Add(TrainingEmails);
        steps.Add(ResearchParticipant);
        steps.Add(CheckYourAnswers);
        return steps;
    }

    public static string? PreviousVisibleStep(User user, string currentStep, IReferenceDataProvider referenceData)
    {
        // Optional sub-steps are reached via in-page links and may not be in the
        // computed sequence yet, so map them back to their parent step.
        if (currentStep == SettingTypeOther)
        {
            return SettingType;
        }

        if (currentStep == RoleTypeOther)
        {
            return RoleType;
        }

        var steps = VisibleSteps(user, referenceData);
        for (var i = 1; i < steps.Count; i++)
        {
            if (string.Equals(steps[i], currentStep, StringComparison.Ordinal))
            {
                return steps[i - 1];
            }
        }

        return null;
    }

    public static string NextStepAfterName() => StepPath(WhereYouLive);

    public static string NextStepAfterWhereYouLive() => StepPath(SettingType);

    public static string NextStepAfterSettingType(User user, SettingTypeOption settingType)
    {
        if (settingType.Id == "other")
        {
            return StepPath(SettingTypeOther);
        }

        return StepPath(NextStepAfterSettingDetails(user, settingType));
    }

    public static string NextStepAfterSettingTypeOther(User user)
    {
        if (IsEngland(user))
        {
            return StepPath(TrainingEmails);
        }

        return StepPath(RoleType);
    }

    public static string NextStepAfterLocalAuthority(User user, SettingTypeOption settingType) =>
        StepPath(NextStepAfterSettingDetails(user, settingType));

    public static string NextStepAfterRole(User user, SettingTypeOption settingType, string roleTypeId)
    {
        if (roleTypeId == "other")
        {
            return StepPath(RoleTypeOther);
        }

        if (RequiresExperienceStep(user, settingType))
        {
            return StepPath(EarlyYearsExperience);
        }

        return StepPath(TrainingEmails);
    }

    public static string NextStepAfterRoleOther(User user, SettingTypeOption settingType)
    {
        if (RequiresExperienceStep(user, settingType))
        {
            return StepPath(EarlyYearsExperience);
        }

        return StepPath(TrainingEmails);
    }

    public static string NextStepAfterEarlyYearsExperience() => StepPath(TrainingEmails);

    public static string NextStepAfterTrainingEmails() => StepPath(ResearchParticipant);

    public static string NextStepAfterResearchParticipant() => StepPath(CheckYourAnswers);

    public static string RoleGroupFor(User user, IReferenceDataProvider referenceData)
    {
        if (user.SettingType == "other")
        {
            return "other";
        }

        return referenceData.GetSettingType(user.SettingType)?.RoleGroup ?? "other";
    }

    private static string NextStepAfterSettingDetails(User user, SettingTypeOption settingType)
    {
        if (LocalAuthorityOutstanding(user, settingType))
        {
            return LocalAuthority;
        }

        if (RoleOutstanding(user, settingType))
        {
            return RoleType;
        }

        return TrainingEmails;
    }
}
