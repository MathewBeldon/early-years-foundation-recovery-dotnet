namespace EarlyYearsFoundationRecovery.Web.Services;

/// <summary>
/// Rails v1.5.0 registration event rows from the registration controllers and
/// Tracking#track. Only the Rails success property is added here; the writer
/// supplies path, controller, and action from the request contract.
/// </summary>
public sealed class RegistrationEventTracker(AuthenticatedKpiEventWriter eventWriter)
{
    public const string TermsAndConditionsEvent = "user_terms_and_conditions_agreed_at_change";
    public const string NameEvent = "user_name_change";
    public const string WhereYouLiveEvent = "user_where_you_live_change";
    public const string SettingTypeEvent = "user_setting_type_change";
    public const string SettingTypeOtherEvent = "user_setting_type_other_change";
    public const string LocalAuthorityEvent = "user_local_authority_change";
    public const string RoleTypeEvent = "user_role_type_change";
    public const string RoleTypeOtherEvent = "user_role_type_other_change";
    public const string EarlyYearsExperienceEvent = "user_early_years_experience_change";
    public const string CheckYourAnswersEvent = "user_check_your_answers";
    public const string RegistrationEvent = "user_registration";

    public Task TrackTermsAndConditionsAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, TermsAndConditionsEvent, "registration/terms_and_conditions", success, cancellationToken);

    public Task TrackNameAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, NameEvent, "registration/names", success, cancellationToken);

    public Task TrackWhereYouLiveAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, WhereYouLiveEvent, "registration/where_you_live", success, cancellationToken);

    public Task TrackSettingTypeAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, SettingTypeEvent, "registration/setting_types", success, cancellationToken);

    public Task TrackSettingTypeOtherAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, SettingTypeOtherEvent, "registration/setting_type_others", success, cancellationToken);

    public Task TrackLocalAuthorityAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, LocalAuthorityEvent, "registration/local_authorities", success, cancellationToken);

    public Task TrackRoleTypeAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, RoleTypeEvent, "registration/role_types", success, cancellationToken);

    public Task TrackRoleTypeOtherAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, RoleTypeOtherEvent, "registration/role_type_others", success, cancellationToken);

    public Task TrackEarlyYearsExperienceAsync(HttpContext context, long userId, bool success, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, EarlyYearsExperienceEvent, "registration/early_years_experiences", success, cancellationToken);

    public Task TrackCheckYourAnswersAsync(HttpContext context, long userId, CancellationToken cancellationToken = default) =>
        TrackAsync(context, userId, CheckYourAnswersEvent, "registration/check_your_answers", success: true, cancellationToken);

    public async Task TrackRegistrationAsync(HttpContext context, long userId, CancellationToken cancellationToken = default)
    {
        var existing = await eventWriter.ListNamedEventsAsync(userId, RegistrationEvent, cancellationToken);
        if (existing.Any(item =>
                item.Properties.TryGetValue("controller", out var controller) &&
                string.Equals(controller?.ToString(), "registration/check_your_answers", StringComparison.Ordinal)))
        {
            return;
        }

        await TrackAsync(
            context,
            userId,
            RegistrationEvent,
            "registration/check_your_answers",
            success: true,
            cancellationToken);
    }

    private Task TrackAsync(
        HttpContext context,
        long userId,
        string eventName,
        string controller,
        bool success,
        CancellationToken cancellationToken) =>
        eventWriter.TrackAsync(
            context,
            userId,
            eventName,
            controller,
            "update",
            cancellationToken,
            new Dictionary<string, object?> { ["success"] = success });
}
