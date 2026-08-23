using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

[Trait("Category", "Database")]
public sealed class LegacyModuleProgressMigrationPostgreSqlTests(PostgreSqlSchemaFixture database)
    : IClassFixture<PostgreSqlSchemaFixture>
{
    [DatabaseFact]
    public async Task Dry_run_is_read_only_and_apply_mirrors_Rails_event_derivation()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.EnsureCreatedAsync();
            var user = new User { Email = "legacy@example.test" };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            var sameTime = Utc(2026, 1, 2, 12, 0);
            setup.Events.AddRange(
                Event(user, "module_start", "module-1", null, Utc(2026, 1, 1, 10, 0)),
                Event(user, "page_view", "module-1", "first", sameTime),
                Event(user, "module_content_page", "module-1", "first", sameTime.AddMinutes(1)),
                Event(user, "page_view", "module-1", "second", sameTime.AddMinutes(2)),
                Event(user, "module_complete", "module-1", null, Utc(2026, 1, 3, 10, 0)),
                Event(user, "page_view", "module-2", "tie-first", Utc(2026, 1, 4, 10, 0)),
                Event(user, "page_view", "module-2", "tie-second", Utc(2026, 1, 4, 10, 0)),
                Event(user, "module_complete", "module-3", null, Utc(2026, 1, 5, 10, 0)));
            await setup.SaveChangesAsync();
        }

        await using (var dryContext = CreateContext(connectionString))
        {
            var dry = await Migration(dryContext).RunAsync(new(false, 1));
            Assert.Equal(1, dry.EligibleUsers);
            Assert.Equal(3, dry.PlannedRows);
            Assert.Equal(0, dry.InsertedRows);
            Assert.Empty(await dryContext.UserModuleProgress.ToListAsync());
        }

        await using (var applyContext = CreateContext(connectionString))
        {
            var applied = await Migration(applyContext).RunAsync(new(true, 1));
            Assert.Equal(3, applied.InsertedRows);
        }

        await using var verify = CreateContext(connectionString);
        var rows = await verify.UserModuleProgress.OrderBy(row => row.ModuleName).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(Utc(2026, 1, 1, 10, 0), rows[0].StartedAt);
        Assert.Equal(Utc(2026, 1, 3, 10, 0), rows[0].CompletedAt);
        Assert.Equal("second", rows[0].LastPage);
        Assert.Equal("2026-01-02T12:00:00Z", rows[0].VisitedPages["first"]);
        Assert.Equal(Utc(2026, 1, 4, 10, 0), rows[1].StartedAt);
        Assert.Equal("tie-second", rows[1].LastPage);
        Assert.Null(rows[2].StartedAt);
        Assert.Equal(Utc(2026, 1, 5, 10, 0), rows[2].CompletedAt);
        Assert.Equal(8, await verify.Events.CountAsync());
    }

    [DatabaseFact]
    public async Task Reports_malformed_unmatched_and_completion_only_without_repairing_them()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.EnsureCreatedAsync();
            var malformed = new User { Email = "malformed@example.test" };
            var completionOnly = new User { Email = "completion@example.test" };
            var existing = new User { Email = "existing@example.test" };
            setup.Users.AddRange(malformed, completionOnly, existing);
            await setup.SaveChangesAsync();
            setup.Events.AddRange(
                Event(malformed, "page_view", "module-1", null, Utc(2026, 2, 1, 10, 0)),
                Event(malformed, "module_start", "retired-module", null, Utc(2026, 2, 1, 11, 0)),
                Event(completionOnly, "module_complete", "module-1", null, Utc(2026, 2, 1, 12, 0)),
                Event(existing, "module_start", "module-1", null, Utc(2026, 2, 1, 13, 0)));
            setup.UserModuleProgress.Add(new UserModuleProgress { User = existing, ModuleName = "module-2" });
            await setup.SaveChangesAsync();
        }

        await using var context = CreateContext(connectionString);
        var report = await Migration(context).RunAsync(new(true, 10));
        Assert.Equal(1, report.EligibleUsers);
        Assert.Equal(1, report.MalformedEvents);
        Assert.Equal(1, report.UnmatchedEvents);
        Assert.Equal(1, report.CompletionOnlyUsers);
        Assert.Equal(0, report.InsertedRows);
        Assert.Single(await context.UserModuleProgress.ToListAsync());
    }

    [DatabaseFact]
    public async Task Repeated_and_concurrent_apply_runs_are_idempotent()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using (var setup = CreateContext(connectionString))
        {
            await setup.Database.EnsureCreatedAsync();
            var user = new User { Email = "concurrent@example.test" };
            setup.Users.Add(user);
            await setup.SaveChangesAsync();
            setup.Events.Add(Event(user, "module_start", "module-1", null, Utc(2026, 3, 1, 10, 0)));
            await setup.SaveChangesAsync();
        }

        await using var firstContext = CreateContext(connectionString);
        await using var secondContext = CreateContext(connectionString);
        var results = await Task.WhenAll(
            Migration(firstContext).RunAsync(new(true, 10)),
            Migration(secondContext).RunAsync(new(true, 10)));
        Assert.Equal(1, results.Sum(result => result.InsertedRows));

        await using var rerunContext = CreateContext(connectionString);
        var rerun = await Migration(rerunContext).RunAsync(new(true, 10));
        Assert.Equal(0, rerun.EligibleUsers);
        Assert.Single(await rerunContext.UserModuleProgress.ToListAsync());
    }

    private static LegacyModuleProgressMigration Migration(ApplicationDbContext context) =>
        new(context, new FixedProvider());

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options,
        new InMemoryNoteBodyProtector());

    private static Event Event(User user, string name, string? module, string? page, DateTime? time)
    {
        var properties = new Dictionary<string, object?>();
        if (module is not null) properties["training_module_id"] = module;
        if (page is not null) properties["id"] = page;
        return new Event { User = user, Name = name, Time = time, Properties = properties };
    }

    private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    private sealed class FixedProvider : ITrainingContentProvider
    {
        private static readonly IReadOnlyList<TrainingModuleContent> Modules =
        [
            new("module-1", "Module 1", "", "", "", 1, 1, true, []),
            new("module-2", "Module 2", "", "", "", 1, 2, true, []),
            new("module-3", "Module 3", "", "", "", 1, 3, true, []),
        ];
        public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Modules);
        public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Modules);
        public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Modules.FirstOrDefault(module => module.Name == moduleName));
        public Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default) => Task.FromResult<TrainingPageContent?>(null);
    }
}
