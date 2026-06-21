using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface ICloseAccountService
{
    Task SaveCloseReasonAsync(
        long userId,
        string closedReason,
        string? closedReasonCustom,
        CancellationToken cancellationToken = default);

    Task<User> RedactAndCloseAsync(long userId, CancellationToken cancellationToken = default);
}
