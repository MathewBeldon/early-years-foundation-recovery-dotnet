using System.Text.Json;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class RegistrationEventTrackerTests
{
    [Fact]
    public async Task Records_the_complete_Rails_registration_event_inventory()
    {
        await using var db = CreateDb();
        var writer = new AuthenticatedKpiEventWriter(db, TimeProvider.System);
        var registration = new RegistrationEventTracker(writer);
        var preferences = new RegistrationPreferenceEventTracker(writer);
        var context = new DefaultHttpContext();
        context.Request.Path = "/registration/check-your-answers";

        await registration.TrackTermsAndConditionsAsync(context, 42, success: false);
        await registration.TrackNameAsync(context, 42, success: true);
        await registration.TrackWhereYouLiveAsync(context, 42, success: true);
        await registration.TrackSettingTypeAsync(context, 42, success: true);
        await registration.TrackSettingTypeOtherAsync(context, 42, success: true);
        await registration.TrackLocalAuthorityAsync(context, 42, success: true);
        await registration.TrackRoleTypeAsync(context, 42, success: true);
        await registration.TrackRoleTypeOtherAsync(context, 42, success: true);
        await registration.TrackEarlyYearsExperienceAsync(context, 42, success: true);
        await preferences.TrackTrainingEmailsAsync(context, 42, success: true);
        await preferences.TrackResearchParticipantAsync(context, 42, success: true);
        await registration.TrackCheckYourAnswersAsync(context, 42);
        await registration.TrackRegistrationAsync(context, 42);

        var events = await db.Events.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(
            [
                (RegistrationEventTracker.TermsAndConditionsEvent, "registration/terms_and_conditions", false),
                (RegistrationEventTracker.NameEvent, "registration/names", true),
                (RegistrationEventTracker.WhereYouLiveEvent, "registration/where_you_live", true),
                (RegistrationEventTracker.SettingTypeEvent, "registration/setting_types", true),
                (RegistrationEventTracker.SettingTypeOtherEvent, "registration/setting_type_others", true),
                (RegistrationEventTracker.LocalAuthorityEvent, "registration/local_authorities", true),
                (RegistrationEventTracker.RoleTypeEvent, "registration/role_types", true),
                (RegistrationEventTracker.RoleTypeOtherEvent, "registration/role_type_others", true),
                (RegistrationEventTracker.EarlyYearsExperienceEvent, "registration/early_years_experiences", true),
                (RegistrationPreferenceEventTracker.TrainingEmailsEvent, "registration/training_emails", true),
                (RegistrationPreferenceEventTracker.ResearchParticipantEvent, "registration/research_participants", true),
                (RegistrationEventTracker.CheckYourAnswersEvent, "registration/check_your_answers", true),
                (RegistrationEventTracker.RegistrationEvent, "registration/check_your_answers", true),
            ],
            events.Select(item =>
                (item.Name!, PropertyString(item.Properties, "controller"), PropertyBool(item.Properties, "success"))));

        Assert.All(events, item =>
        {
            Assert.Equal("update", PropertyString(item.Properties, "action"));
            Assert.Equal("/registration/check-your-answers", PropertyString(item.Properties, "path"));
            Assert.Equal(4, item.Properties.Count);
        });
    }

    [Fact]
    public async Task Registration_completion_event_is_idempotent_for_replayed_submission()
    {
        await using var db = CreateDb();
        var tracker = new RegistrationEventTracker(new AuthenticatedKpiEventWriter(db, TimeProvider.System));
        var context = new DefaultHttpContext();
        context.Request.Path = "/registration/check-your-answers";

        await tracker.TrackRegistrationAsync(context, 42);
        await tracker.TrackRegistrationAsync(context, 42);

        var recorded = Assert.Single(await db.Events.AsNoTracking().ToListAsync());
        Assert.Equal(RegistrationEventTracker.RegistrationEvent, recorded.Name);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options, new InMemoryNoteBodyProtector());
    }

    private static string PropertyString(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties[key] switch
        {
            string value => value,
            JsonElement element => element.GetString() ?? string.Empty,
            { } value => value.ToString() ?? string.Empty,
            null => string.Empty,
        };

    private static bool PropertyBool(IReadOnlyDictionary<string, object?> properties, string key) =>
        properties[key] switch
        {
            bool value => value,
            JsonElement element => element.GetBoolean(),
            _ => throw new InvalidOperationException($"Property '{key}' was not boolean."),
        };
}
