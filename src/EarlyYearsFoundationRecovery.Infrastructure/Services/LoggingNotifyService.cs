using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class LoggingNotifyService(
    ApplicationDbContext dbContext,
    ILogger<LoggingNotifyService> logger) : INotifyService
{
    public async Task SendEmailAsync(
        string templateId,
        string recipientEmail,
        IReadOnlyDictionary<string, object?> personalisation,
        long userId,
        CancellationToken cancellationToken = default)
    {
        // Avoid logging recipient email or personalisation values (PII). Keys are safe and enough to debug.
        logger.LogInformation(
            "Stub Notify: template={TemplateId} userId={UserId} personalisationKeys={PersonalisationKeys}",
            templateId,
            userId,
            string.Join(", ", personalisation.Keys));

        dbContext.MailEvents.Add(new MailEvent
        {
            UserId = userId,
            Template = templateId,
            Personalisation = personalisation.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
