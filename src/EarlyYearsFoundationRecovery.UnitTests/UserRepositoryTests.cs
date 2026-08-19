using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Auth;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class UserRepositoryTests
{
    [Fact]
    public async Task FindOrCreateFromGovOneAsync_backfills_legacy_user_matched_by_email()
    {
        // Rails v1.5.0 ac546721 app/models/user.rb:37-40 matches email first and backfills a null gov_one_id.
        await using var db = CreateDbContext();
        var legacyUser = ExistingUser("legacy@example.com", govOneId: null);
        db.Users.Add(legacyUser);
        await db.SaveChangesAsync();

        var result = await new UserRepository(db)
            .FindOrCreateFromGovOneAsync("legacy@example.com", "gov-one-legacy");

        Assert.True(result.Id == legacyUser.Id, "The legacy account should be returned after its GOV.UK One Login id is backfilled.");
        Assert.Equal("gov-one-legacy", result.GovOneId);
    }

    [Fact]
    public async Task FindOrCreateFromGovOneAsync_does_not_duplicate_user_matched_by_email()
    {
        // Rails v1.5.0 ac546721 app/models/user.rb:38-40 updates the email match instead of creating another user.
        await using var db = CreateDbContext();
        var existingUser = ExistingUser("existing@example.com", govOneId: null);
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var result = await new UserRepository(db)
            .FindOrCreateFromGovOneAsync("existing@example.com", "gov-one-existing");

        Assert.True(result.Id == existingUser.Id, "Signing in should retain the existing user id.");
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task FindOrCreateFromGovOneAsync_does_not_overwrite_existing_gov_one_id()
    {
        // Rails v1.5.0 ac546721 app/models/user.rb:38-40 gives the email match priority and only fills a null gov_one_id.
        await using var db = CreateDbContext();
        var existingUser = ExistingUser("existing@example.com", "original-gov-one-id");
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var result = await new UserRepository(db)
            .FindOrCreateFromGovOneAsync("existing@example.com", "different-gov-one-id");

        Assert.Equal("original-gov-one-id", result.GovOneId);
        Assert.Single(await db.Users.ToListAsync());
    }

    [Fact]
    public async Task FindOrCreateFromGovOneAsync_does_not_change_updated_at_for_existing_user()
    {
        // Rails v1.5.0 ac546721 app/models/user.rb:39-40 uses update_column, which does not change updated_at.
        await using var db = CreateDbContext();
        var existingUser = ExistingUser("old@example.com", "gov-one-id");
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();
        var originalUpdatedAt = existingUser.UpdatedAt;

        var result = await new UserRepository(db)
            .FindOrCreateFromGovOneAsync("new@example.com", "gov-one-id");

        Assert.Equal("new@example.com", result.Email);
        Assert.True(result.UpdatedAt == originalUpdatedAt, "The GOV.UK One Login match-and-update path must preserve users.updated_at.");
    }

    [Fact]
    public async Task FindOrCreateFromGovOneAsync_confirms_new_user()
    {
        // Rails v1.5.0 ac546721 app/models/user.rb:41-47 sets confirmed_at when creating a GOV.UK One Login user.
        await using var db = CreateDbContext();

        var result = await new UserRepository(db)
            .FindOrCreateFromGovOneAsync("new@example.com", "new-gov-one-id");

        Assert.NotNull(result.ConfirmedAt);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options, new InMemoryNoteBodyProtector());
    }

    private static User ExistingUser(string email, string? govOneId) => new()
    {
        Email = email,
        GovOneId = govOneId,
        RegistrationComplete = false,
    };
}
