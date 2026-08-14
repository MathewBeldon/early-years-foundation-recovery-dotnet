using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class NotifyCallbackHandler(ApplicationDbContext dbContext) : INotifyCallbackHandler
{
    public async Task<bool> HandleAsync(string payload, CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("to", out var to))
        {
            return false;
        }

        var email = to.GetString();
        var templateId = root.TryGetProperty("template_id", out var template)
            ? template.GetString()
            : root.TryGetProperty("template", out var legacyTemplate) ? legacyTemplate.GetString() : null;
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var callback = JsonSerializer.Deserialize<Dictionary<string, object?>>(payload) ?? [];
        user.NotifyCallback = callback;
        var notificationId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
        var mailEvent = await dbContext.MailEvents
            .Where(x => x.UserId == user.Id && x.Template == templateId && x.Callback == null &&
                (notificationId == null || x.NotificationId == notificationId))
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (mailEvent is not null)
        {
            mailEvent.Callback = callback;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
