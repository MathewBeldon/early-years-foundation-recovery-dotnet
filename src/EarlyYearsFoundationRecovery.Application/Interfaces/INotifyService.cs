namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface INotifyService
{
    Task SendEmailAsync(
        string templateId,
        string recipientEmail,
        IReadOnlyDictionary<string, object?> personalisation,
        long userId,
        CancellationToken cancellationToken = default);
}
