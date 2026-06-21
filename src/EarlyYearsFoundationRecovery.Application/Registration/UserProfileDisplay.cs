using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Registration;

public static class UserProfileDisplay
{
    public static string FullName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(static part => !string.IsNullOrWhiteSpace(part)));

    public static string CountryName(User user) => user.Country ?? string.Empty;

    public static string SettingName(User user, IReferenceDataProvider referenceData)
    {
        if (user.SettingType == "other")
        {
            return user.SettingTypeOther ?? string.Empty;
        }

        return referenceData.GetSettingType(user.SettingType)?.Label ?? user.SettingType ?? string.Empty;
    }

    public static string AuthorityName(User user)
    {
        if (!RegistrationJourney.IsEngland(user))
        {
            return RegistrationJourney.NotApplicable;
        }

        if (RegistrationJourney.IsNotApplicable(user.LocalAuthority))
        {
            return RegistrationJourney.NotApplicable;
        }

        return string.IsNullOrWhiteSpace(user.LocalAuthority)
            ? RegistrationJourney.MultipleLocalAuthorities
            : user.LocalAuthority;
    }

    public static string RoleName(User user, IReferenceDataProvider referenceData)
    {
        if (RegistrationJourney.IsNotApplicable(user.RoleType))
        {
            return RegistrationJourney.NotApplicable;
        }

        if (user.RoleType == "other")
        {
            return user.RoleTypeOther ?? string.Empty;
        }

        return referenceData.GetRole(user.RoleType)?.Label ?? user.RoleType ?? string.Empty;
    }

    public static string ExperienceName(User user, IReferenceDataProvider referenceData)
    {
        if (string.IsNullOrWhiteSpace(user.EarlyYearsExperience))
        {
            return string.Empty;
        }

        return referenceData.GetExperienceLevel(user.EarlyYearsExperience)?.Label
            ?? user.EarlyYearsExperience;
    }

    public static bool ShowsExperience(User user, IReferenceDataProvider referenceData)
    {
        var settingType = referenceData.GetSettingType(user.SettingType);
        return settingType is not null &&
               RegistrationJourney.RequiresExperienceStep(user, settingType) &&
               !string.IsNullOrWhiteSpace(user.EarlyYearsExperience);
    }

    public static bool TrainingEmailsRecipient(User user) => user.TrainingEmails == true;

    public static string TrainingEmailsPreferenceText(User user) =>
        TrainingEmailsRecipient(user)
            ? "You have chosen to receive emails about this training course."
            : "You have chosen not to receive emails about this training course.";

    public static string ResearchPreferenceText(User user) =>
        user.ResearchParticipant == true
            ? "You have chosen to participate in research."
            : "You have chosen not to participate in research.";
}
