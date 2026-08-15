using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Auth;

public sealed class UserRepository(ApplicationDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByGovOneIdAsync(string govOneId, CancellationToken cancellationToken = default) =>
        dbContext.Users.FirstOrDefaultAsync(u => u.GovOneId == govOneId, cancellationToken);

    public async Task<User> FindOrCreateFromGovOneAsync(
        string email,
        string govOneId,
        CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken)
            ?? await GetByGovOneIdAsync(govOneId, cancellationToken);
        if (existing is not null)
        {
            existing.Email = email;
            if (existing.GovOneId is null)
            {
                existing.GovOneId = govOneId;
            }

            await dbContext.SaveChangesWithoutApplyingTimestampsAsync(cancellationToken);
            return existing;
        }

        var user = new User
        {
            Email = email,
            GovOneId = govOneId,
            ConfirmedAt = DateTime.UtcNow,
            RegistrationComplete = false,
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task SaveAsync(User user, CancellationToken cancellationToken = default)
    {
        if (user.Id == 0)
        {
            dbContext.Users.Add(user);
        }
        else if (dbContext.Entry(user).State == EntityState.Detached)
        {
            dbContext.Users.Update(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
