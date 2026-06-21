using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyYearsFoundationRecovery.Infrastructure.Training;

public sealed class UserModuleProgressRepository(ApplicationDbContext dbContext) : IUserModuleProgressRepository
{
    public async Task<IReadOnlyList<UserModuleProgress>> GetForUserAsync(long userId, CancellationToken cancellationToken = default) =>
        await dbContext.UserModuleProgress
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task<UserModuleProgress?> GetAsync(long userId, string moduleName, bool asNoTracking = false, CancellationToken cancellationToken = default) =>
        (asNoTracking ? dbContext.UserModuleProgress.AsNoTracking() : dbContext.UserModuleProgress)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.ModuleName == moduleName, cancellationToken);

    public async Task<UserModuleProgress> GetOrCreateAsync(long userId, string moduleName, CancellationToken cancellationToken = default)
    {
        var existing = await GetAsync(userId, moduleName, cancellationToken: cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var progress = new UserModuleProgress
        {
            UserId = userId,
            ModuleName = moduleName,
        };

        dbContext.UserModuleProgress.Add(progress);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return progress;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(progress).State = EntityState.Detached;
            var createdByConcurrentRequest = await GetAsync(userId, moduleName, cancellationToken: cancellationToken);
            if (createdByConcurrentRequest is null)
            {
                throw;
            }

            return createdByConcurrentRequest;
        }
    }

    public async Task SaveAsync(UserModuleProgress progress, CancellationToken cancellationToken = default)
    {
        if (progress.Id == 0)
        {
            dbContext.UserModuleProgress.Add(progress);
        }
        else if (dbContext.Entry(progress).State == EntityState.Detached)
        {
            dbContext.UserModuleProgress.Update(progress);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
