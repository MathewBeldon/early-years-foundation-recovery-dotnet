using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Notify;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Jobs;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class NewModuleNotificationDeliveryJobTests
{
    [Fact]
    public async Task Successful_delivery_uses_Rails_personalisation_records_mail_event_and_closes_ledger()
    {
        await using var db = CreateContext();
        var delivery = await SeedAsync(db);
        var notify = new RecordingNotify(db);

        await new NewModuleNotificationDeliveryJob(db, notify, TimeProvider.System)
            .RunAsync($"{{\"deliveryId\":{delivery.Id}}}");

        Assert.Equal(1, notify.Calls);
        Assert.Equal(4, notify.Personalisation!["mod_number"]);
        Assert.Equal("Communication and language", notify.Personalisation["mod_name"]);
        Assert.Equal("Criteria", notify.Personalisation["mod_criteria"]);
        Assert.Equal("https://training.example.test/", notify.Personalisation["url"]);
        Assert.Equal("delivered", delivery.Status);
        Assert.NotNull(delivery.DeliveredAt);
        Assert.Single(await db.MailEvents.ToListAsync());
    }

    [Fact]
    public async Task Existing_Rails_mail_event_closes_ledger_without_resending()
    {
        await using var db = CreateContext();
        var delivery = await SeedAsync(db);
        db.MailEvents.Add(new MailEvent
        {
            UserId = delivery.UserId,
            Template = NotifyTemplateIds.NewModule,
            Personalisation = new Dictionary<string, object?> { ["mod_number"] = 4 },
        });
        await db.SaveChangesAsync();
        var notify = new RecordingNotify(db);

        await new NewModuleNotificationDeliveryJob(db, notify, TimeProvider.System)
            .RunAsync($"{{\"deliveryId\":{delivery.Id}}}");

        Assert.Equal(0, notify.Calls);
        Assert.Equal("delivered", delivery.Status);
    }

    [Fact]
    public async Task Mail_event_for_an_older_module_does_not_suppress_delivery()
    {
        await using var db = CreateContext();
        var delivery = await SeedAsync(db);
        db.MailEvents.Add(new MailEvent
        {
            UserId = delivery.UserId,
            Template = NotifyTemplateIds.NewModule,
            Personalisation = new Dictionary<string, object?> { ["mod_number"] = 3 },
        });
        await db.SaveChangesAsync();
        var notify = new RecordingNotify(db);

        await new NewModuleNotificationDeliveryJob(db, notify, TimeProvider.System)
            .RunAsync($"{{\"deliveryId\":{delivery.Id}}}");

        Assert.Equal(1, notify.Calls);
        Assert.Equal("delivered", delivery.Status);
    }

    [Fact]
    public async Task Notify_failure_leaves_only_that_delivery_pending_for_job_retry()
    {
        await using var db = CreateContext();
        var delivery = await SeedAsync(db);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new NewModuleNotificationDeliveryJob(db, new RecordingNotify(db, fail: true), TimeProvider.System)
                .RunAsync($"{{\"deliveryId\":{delivery.Id}}}"));

        Assert.Equal("pending", delivery.Status);
        Assert.Null(delivery.DeliveredAt);
        Assert.Empty(await db.MailEvents.ToListAsync());
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
        new InMemoryNoteBodyProtector());

    private static async Task<NewModuleNotificationDelivery> SeedAsync(ApplicationDbContext db)
    {
        var user = new User { Email = "person@example.test" };
        var release = new Release { Name = "release", Time = DateTime.UtcNow };
        db.AddRange(user, release);
        await db.SaveChangesAsync();
        var module = new ModuleRelease { ReleaseId = release.Id, Name = "module-4", ModulePosition = 4, FirstPublishedAt = release.Time };
        db.ModuleReleases.Add(module);
        await db.SaveChangesAsync();
        var delivery = new NewModuleNotificationDelivery
        {
            ModuleReleaseId = module.Id, UserId = user.Id, TemplateId = NotifyTemplateIds.NewModule,
            ModulePosition = 4, ModuleTitle = "Communication and language", ModuleCriteria = "Criteria",
            PublicUrl = "https://training.example.test/",
        };
        db.NewModuleNotificationDeliveries.Add(delivery);
        await db.SaveChangesAsync();
        return delivery;
    }

    private sealed class RecordingNotify(ApplicationDbContext db, bool fail = false) : INotifyService
    {
        public int Calls { get; private set; }
        public IReadOnlyDictionary<string, object?>? Personalisation { get; private set; }
        public async Task SendEmailAsync(string templateId, string recipientEmail,
            IReadOnlyDictionary<string, object?> personalisation, long userId,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Personalisation = personalisation;
            if (fail) throw new HttpRequestException("Notify unavailable");
            db.MailEvents.Add(new MailEvent { UserId = userId, Template = templateId, Personalisation = personalisation.ToDictionary() });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
