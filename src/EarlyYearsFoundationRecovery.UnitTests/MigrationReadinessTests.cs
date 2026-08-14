using System.Net;
using System.Text;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class MigrationReadinessTests
{
    [Fact]
    public async Task Durable_job_service_persists_serialized_work()
    {
        await using var db = NewDb();
        var service = new PostgresBackgroundJobService(db);

        var id = await service.EnqueueAsync("dashboard_export", new { requestedBy = "test" });

        var job = await db.BackgroundJobs.SingleAsync();
        Assert.Equal(id, job.Id);
        Assert.Equal("queued", job.Status);
        Assert.Contains("requestedBy", job.Payload);
    }

    [Fact]
    public async Task Notify_send_uses_http_protocol_and_persists_notification_id()
    {
        await using var db = NewDb();
        var user = new User { Email = "notify@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var handler = new RecordingHandler("{\"id\":\"notification-123\"}");
        var service = new HttpNotifyService(new HttpClient(handler) { BaseAddress = new Uri("http://notify.test/") }, db);

        await service.SendEmailAsync("template-1", user.Email, new Dictionary<string, object?> { ["name"] = "Example" }, user.Id);

        Assert.Equal("/v2/notifications/email", handler.RequestUri?.AbsolutePath);
        var mailEvent = await db.MailEvents.SingleAsync();
        Assert.Equal("notification-123", mailEvent.NotificationId);
        Assert.Null(mailEvent.Callback);
    }

    [Fact]
    public async Task Notify_callback_updates_user_and_matching_mail_event()
    {
        await using var db = NewDb();
        var user = new User { Email = "callback@example.test" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.MailEvents.Add(new MailEvent
        {
            UserId = user.Id,
            Template = "template-1",
            NotificationId = "notification-123",
        });
        await db.SaveChangesAsync();

        var matched = await new NotifyCallbackHandler(db).HandleAsync(
            "{\"id\":\"notification-123\",\"to\":\"callback@example.test\",\"template_id\":\"template-1\",\"status\":\"delivered\"}");

        Assert.True(matched);
        Assert.Equal("delivered", Convert.ToString(user.NotifyCallback!["status"]));
        Assert.NotNull((await db.MailEvents.SingleAsync()).Callback);
    }

    [Fact]
    public void Rails_owned_models_map_to_authoritative_table_names()
    {
        using var db = NewDb();
        Assert.Equal("confidence_check_progress", db.Model.FindEntityType(typeof(ConfidenceCheckProgress))!.GetTableName());
        Assert.Equal("module_releases", db.Model.FindEntityType(typeof(ModuleRelease))!.GetTableName());
        Assert.Equal("releases", db.Model.FindEntityType(typeof(Release))!.GetTableName());
        Assert.Equal("background_jobs", db.Model.FindEntityType(typeof(BackgroundJob))!.GetTableName());
    }

    private static ApplicationDbContext NewDb() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class RecordingHandler(string response) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(response, Encoding.UTF8, "application/json"),
            });
        }
    }
}
