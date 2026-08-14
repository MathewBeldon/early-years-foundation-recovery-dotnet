using System.Net.Http.Json;
using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class HttpNotifyService(HttpClient httpClient, ApplicationDbContext dbContext) : INotifyService
{
    public async Task SendEmailAsync(
        string templateId,
        string recipientEmail,
        IReadOnlyDictionary<string, object?> personalisation,
        long userId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("v2/notifications/email", new
        {
            email_address = recipientEmail,
            template_id = templateId,
            personalisation,
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
        var notificationId = document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;

        dbContext.MailEvents.Add(new MailEvent
        {
            UserId = userId,
            Template = templateId,
            NotificationId = notificationId,
            Personalisation = personalisation.ToDictionary(),
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
