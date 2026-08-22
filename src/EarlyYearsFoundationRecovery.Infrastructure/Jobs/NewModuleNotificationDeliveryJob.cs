using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

internal sealed class NewModuleNotificationDeliveryJob(
    ApplicationDbContext db,
    INotifyService notify,
    TimeProvider timeProvider)
{
    public const string JobType = "new_module_notification_delivery";

    public async Task RunAsync(string payload, CancellationToken cancellationToken = default)
    {
        var deliveryId = JsonSerializer.Deserialize<DeliveryPayload>(payload)?.DeliveryId
            ?? throw new InvalidOperationException("new_module_notification_delivery payload has no deliveryId.");
        var delivery = await db.NewModuleNotificationDeliveries
            .Include(x => x.User).Include(x => x.ModuleRelease)
            .SingleOrDefaultAsync(x => x.Id == deliveryId, cancellationToken)
            ?? throw new InvalidOperationException($"Notification delivery {deliveryId} does not exist.");
        if (delivery.Status == "delivered") return;

        // HttpNotifyService records the Rails-compatible event after Notify accepts the
        // request. A retry can therefore close the ledger without another network call.
        var matchingEvents = await db.MailEvents
            .Where(x => x.UserId == delivery.UserId && x.Template == delivery.TemplateId)
            .Select(x => x.Personalisation)
            .ToListAsync(cancellationToken);
        if (matchingEvents.Any(x => HasModulePosition(x, delivery.ModulePosition)))
        {
            await MarkDeliveredAsync(delivery, cancellationToken);
            return;
        }

        await notify.SendEmailAsync(delivery.TemplateId, delivery.User.Email,
            new Dictionary<string, object?>
            {
                ["mod_number"] = delivery.ModulePosition,
                ["mod_name"] = delivery.ModuleTitle,
                ["mod_criteria"] = delivery.ModuleCriteria,
                ["url"] = delivery.PublicUrl,
            }, delivery.UserId, cancellationToken);
        await MarkDeliveredAsync(delivery, cancellationToken);
    }

    private static bool HasModulePosition(IReadOnlyDictionary<string, object?> personalisation, int modulePosition)
    {
        if (!personalisation.TryGetValue("mod_number", out var value) || value is null) return false;
        return value switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Number =>
                element.TryGetInt32(out var number) && number == modulePosition,
            _ => int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out var number)
                && number == modulePosition,
        };
    }

    private async Task MarkDeliveredAsync(NewModuleNotificationDelivery delivery, CancellationToken cancellationToken)
    {
        delivery.Status = "delivered";
        delivery.DeliveredAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record DeliveryPayload([property: JsonPropertyName("deliveryId")] long DeliveryId);
}
