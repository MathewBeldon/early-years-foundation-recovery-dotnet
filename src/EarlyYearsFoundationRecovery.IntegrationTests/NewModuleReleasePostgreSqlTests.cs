using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Notify;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure;
using EarlyYearsFoundationRecovery.Infrastructure.Jobs;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

[Trait("Category", "Database")]
public sealed class NewModuleReleasePostgreSqlTests(PostgreSqlSchemaFixture database)
    : IClassFixture<PostgreSqlSchemaFixture>
{
    [DatabaseFact]
    public async Task Concurrent_duplicate_jobs_create_one_reservation_campaign_and_recipient_job()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.Add(new User { Email = "eligible@example.test", TrainingEmails = null });
            var previousAnnouncement = new User { Email = "previous@example.test", TrainingEmails = true };
            var currentAnnouncement = new User { Email = "current@example.test", TrainingEmails = true };
            setup.Users.AddRange(previousAnnouncement, currentAnnouncement);
            setup.Users.Add(new User { Email = "opted-out@example.test", TrainingEmails = false });
            setup.Users.Add(new User { Email = "closed@example.test", ClosedAt = DateTime.UtcNow });
            setup.Releases.AddRange(
                new Release { Name = "release-a", Time = DateTime.UtcNow.AddMinutes(-2) },
                new Release { Name = "release-b", Time = DateTime.UtcNow.AddMinutes(-1) });
            await setup.SaveChangesAsync();
            setup.MailEvents.AddRange(
                new MailEvent
                {
                    UserId = previousAnnouncement.Id,
                    Template = NotifyTemplateIds.NewModule,
                    Personalisation = new Dictionary<string, object?> { ["mod_number"] = 3 },
                },
                new MailEvent
                {
                    UserId = currentAnnouncement.Id,
                    Template = NotifyTemplateIds.NewModule,
                    Personalisation = new Dictionary<string, object?> { ["mod_number"] = 4 },
                });
            await setup.SaveChangesAsync();
        }

        await using var first = CreateContext(connectionString);
        await using var second = CreateContext(connectionString);
        var releaseIds = await first.Releases.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();
        var provider = new FixedTrainingProvider(Module());
        var runA = CreateJob(first, provider).RunAsync($"{{\"releaseId\":{releaseIds[0]}}}");
        var runB = CreateJob(second, provider).RunAsync($"{{\"releaseId\":{releaseIds[1]}}}");
        await Task.WhenAll(runA, runB);

        await using var verify = CreateContext(connectionString);
        var reservation = await verify.ModuleReleases.SingleAsync();
        Assert.Contains(reservation.ReleaseId, releaseIds);
        Assert.Equal(2, await verify.NewModuleNotificationDeliveries.CountAsync());
        Assert.Equal(2, await verify.BackgroundJobs.CountAsync(x => x.JobType == NewModuleNotificationDeliveryJob.JobType));
    }

    [DatabaseFact]
    public async Task One_dimension_conflict_rolls_back_campaign_and_preserves_first_release()
    {
        var connectionString = await database.CreateDatabaseAsync();
        long firstReleaseId;
        long secondReleaseId;
        var published = DateTime.UtcNow.AddDays(-1);
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.EnsureCreatedAsync();
            var firstRelease = new Release { Name = "first", Time = published };
            var secondRelease = new Release { Name = "second", Time = DateTime.UtcNow };
            setup.Releases.AddRange(firstRelease, secondRelease);
            await setup.SaveChangesAsync();
            firstReleaseId = firstRelease.Id;
            secondReleaseId = secondRelease.Id;
            setup.ModuleReleases.Add(new ModuleRelease
            {
                ReleaseId = firstReleaseId, Name = "module-4", ModulePosition = 3, FirstPublishedAt = published,
            });
            await setup.SaveChangesAsync();
        }

        await using (var context = CreateContext(connectionString))
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                CreateJob(context, new FixedTrainingProvider(Module())).RunAsync($"{{\"releaseId\":{secondReleaseId}}}"));
            Assert.Contains("integrity conflict", exception.Message, StringComparison.OrdinalIgnoreCase);
        }

        await using var verify = CreateContext(connectionString);
        var existing = await verify.ModuleReleases.SingleAsync();
        Assert.Equal(firstReleaseId, existing.ReleaseId);
        Assert.InRange((existing.FirstPublishedAt - published).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        Assert.Empty(await verify.NewModuleNotificationDeliveries.ToListAsync());
        Assert.Empty(await verify.BackgroundJobs.ToListAsync());
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options,
        new InMemoryNoteBodyProtector());

    private static NewModuleReleaseJob CreateJob(ApplicationDbContext context, ITrainingContentProvider provider) =>
        new(context, provider, Options.Create(new InfrastructureOptions { PublicBaseUrl = "https://training.example.test" }), TimeProvider.System);

    private static TrainingModuleContent Module() => new(
        "module-4", "Communication and language", "Description", "Outcomes", "Criteria", 2, 4, true, []);

    private sealed class FixedTrainingProvider(TrainingModuleContent module) : ITrainingContentProvider
    {
        public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingModuleContent>>([module]);
        public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) => GetLiveModulesAsync(cancellationToken);
        public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default) => Task.FromResult<TrainingModuleContent?>(module);
        public Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default) => Task.FromResult<TrainingPageContent?>(null);
    }
}
