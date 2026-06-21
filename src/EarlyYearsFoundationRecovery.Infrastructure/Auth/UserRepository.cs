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
        var existing = await GetByGovOneIdAsync(govOneId, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                existing.Email = email;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return existing;
        }

        var user = new User
        {
            Email = email,
            GovOneId = govOneId,
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
