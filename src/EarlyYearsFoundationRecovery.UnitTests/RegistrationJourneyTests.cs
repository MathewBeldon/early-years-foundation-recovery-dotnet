using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class RegistrationJourneyTests
{
    private static readonly TestReferenceData ReferenceData = new();

    [Fact]
    public void ResolveCurrentStep_starts_at_terms_for_new_user()
    {
        var user = new User();
        Assert.Equal(RegistrationJourney.TermsAndConditions, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void England_nursery_flow_includes_local_authority_role_and_experience()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "nursery";

        Assert.Equal(RegistrationJourney.LocalAuthority, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));

        user.LocalAuthority = "Leeds";
        Assert.Equal(RegistrationJourney.RoleType, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));

        user.RoleType = "student";
        Assert.Equal(RegistrationJourney.EarlyYearsExperience, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));

        user.EarlyYearsExperience = "2-5";
        Assert.Equal(RegistrationJourney.TrainingEmails, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));

        user.TrainingEmails = true;
        // Rails v1.5.0 registration/base_controller.rb treats research
        // participation as optional when resuming.
        Assert.Equal(RegistrationJourney.CheckYourAnswers, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));

        user.ResearchParticipant = true;
        Assert.Equal(RegistrationJourney.CheckYourAnswers, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void First_time_linear_navigation_still_routes_to_research_participant_after_training_emails()
    {
        // Rails v1.5.0 registration/training_emails_controller.rb preserves
        // the research page in the first-time linear journey.
        Assert.Equal(
            RegistrationJourney.StepPath(RegistrationJourney.ResearchParticipant),
            RegistrationJourney.NextStepAfterTrainingEmails());
    }

    [Fact]
    public void England_local_authority_setting_skips_role_and_experience()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "local_authority";

        Assert.Equal(RegistrationJourney.LocalAuthority, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));

        user.LocalAuthority = "Leeds";
        Assert.Equal(RegistrationJourney.TrainingEmails, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void England_department_for_education_skips_to_training_emails()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "department_for_education";

        Assert.Equal(RegistrationJourney.TrainingEmails, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void Scotland_nursery_skips_local_authority()
    {
        var user = NamedUser(country: "Scotland");
        user.SettingTypeId = "nursery";

        Assert.Equal(RegistrationJourney.RoleType, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void Scotland_department_for_education_skips_to_training_emails()
    {
        var user = NamedUser(country: "Scotland");
        user.SettingTypeId = "department_for_education";

        Assert.Equal(RegistrationJourney.TrainingEmails, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void England_other_setting_skips_role_after_custom_text()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "other";
        user.SettingTypeOther = "user defined setting";
        user.LocalAuthority = RegistrationJourney.NotApplicable;
        user.RoleType = RegistrationJourney.NotApplicable;

        Assert.Equal(RegistrationJourney.TrainingEmails, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void Scotland_other_setting_requires_role()
    {
        var user = NamedUser(country: "Scotland");
        user.SettingTypeId = "other";
        user.SettingTypeOther = "user defined setting";
        user.LocalAuthority = RegistrationJourney.NotApplicable;

        Assert.Equal(RegistrationJourney.RoleType, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void Resume_returns_check_your_answers_when_all_answers_present()
    {
        var user = FullyAnsweredEnglandNursery();

        Assert.Equal(RegistrationJourney.CheckYourAnswers, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void ResolveCurrentStep_does_not_infer_identifier_from_legacy_snapshot()
    {
        var user = NamedUser(country: "England");
        user.SettingType = "Private nursery";

        Assert.Equal(RegistrationJourney.SettingType, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
        Assert.Null(user.SettingTypeId);
        Assert.Equal("Private nursery", user.SettingType);
    }

    [Fact]
    public void ResolveCurrentStep_routes_using_identifier_when_snapshot_disagrees()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "department_for_education";
        user.SettingType = "other";
        user.LocalAuthority = RegistrationJourney.NotApplicable;
        user.RoleType = RegistrationJourney.NotApplicable;
        user.TrainingEmails = true;

        Assert.Equal(RegistrationJourney.CheckYourAnswers, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void Changing_setting_type_during_review_resets_downstream_and_resumes_at_local_authority()
    {
        var user = FullyAnsweredEnglandNursery();

        // Re-selecting a setting type clears the dependent answers (as the command does).
        RegistrationJourney.ApplySettingTypeReset(user, ReferenceData.GetSettingType("nursery")!);

        // The user must be sent back through the now-outstanding steps before the summary.
        Assert.Equal(RegistrationJourney.LocalAuthority, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void Changing_to_no_role_setting_during_review_resumes_at_check_your_answers()
    {
        var user = FullyAnsweredEnglandNursery();
        user.SettingTypeId = "department_for_education";

        // Setting with no local authority and no role: dependent steps become "Not applicable".
        RegistrationJourney.ApplySettingTypeReset(user, ReferenceData.GetSettingType("department_for_education")!);

        // Email and research preferences are still answered, so the user goes straight back to the summary.
        Assert.Equal(RegistrationJourney.CheckYourAnswers, RegistrationJourney.ResolveCurrentStep(user, ReferenceData));
    }

    [Fact]
    public void NextStepAfterLocalAuthority_does_not_skip_role_when_authority_skipped()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "nursery";
        user.LocalAuthority = RegistrationJourney.MultipleLocalAuthorities;

        var next = RegistrationJourney.NextStepAfterLocalAuthority(user, ReferenceData.GetSettingType("nursery")!);
        Assert.Equal(RegistrationJourney.StepPath(RegistrationJourney.RoleType), next);
    }

    private static User FullyAnsweredEnglandNursery()
    {
        var user = NamedUser(country: "England");
        user.SettingTypeId = "nursery";
        user.LocalAuthority = "Leeds";
        user.RoleType = "student";
        user.EarlyYearsExperience = "2-5";
        user.TrainingEmails = true;
        user.ResearchParticipant = true;
        return user;
    }

    private static User NamedUser(string country)
    {
        return new User
        {
            TermsAndConditionsAgreedAt = DateTime.UtcNow,
            FirstName = "Jane",
            LastName = "Doe",
            Country = country,
        };
    }

    private sealed class TestReferenceData : IReferenceDataProvider
    {
        public IReadOnlyList<ReferenceOption> Countries { get; } =
        [
            new("england", "England"),
            new("scotland", "Scotland"),
        ];

        public IReadOnlyList<SettingTypeOption> SettingTypes { get; } =
        [
            new("nursery", "Private nursery", true, "other"),
            new("local_authority", "Local authority", true, "none"),
            new("department_for_education", "Department for Education", false, "none"),
            new("other", "Other", false, "other"),
        ];

        public IReadOnlyList<RoleOption> Roles { get; } =
        [
            new("student", "Student", "other"),
            new("other", "Other", "other"),
        ];

        public IReadOnlyList<ReferenceOption> LocalAuthorities { get; } = [new("leeds", "Leeds")];
        public IReadOnlyList<ReferenceOption> ExperienceLevels { get; } = [new("2-5", "Between 2 and 5 years")];

        public ReferenceOption? GetCountry(string? id) => Countries.FirstOrDefault(x => x.Id == id);
        public SettingTypeOption? GetSettingType(string? id) => SettingTypes.FirstOrDefault(x => x.Id == id);
        public RoleOption? GetRole(string? id) => Roles.FirstOrDefault(x => x.Id == id);
        public ReferenceOption? GetLocalAuthority(string? id) => LocalAuthorities.FirstOrDefault(x => x.Id == id);
        public ReferenceOption? GetExperienceLevel(string? id) => ExperienceLevels.FirstOrDefault(x => x.Id == id);
        public IReadOnlyList<RoleOption> GetRolesForGroup(string? group) => Roles.Where(x => x.Group == group).ToList();
    }
}
