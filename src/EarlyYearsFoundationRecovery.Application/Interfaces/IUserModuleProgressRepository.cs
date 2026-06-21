using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IUserModuleProgressRepository
{
    Task<IReadOnlyList<UserModuleProgress>> GetForUserAsync(long userId, CancellationToken cancellationToken = default);
    Task<UserModuleProgress?> GetAsync(long userId, string moduleName, bool asNoTracking = false, CancellationToken cancellationToken = default);
    Task<UserModuleProgress> GetOrCreateAsync(long userId, string moduleName, CancellationToken cancellationToken = default);
    Task SaveAsync(UserModuleProgress progress, CancellationToken cancellationToken = default);
}
