using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class NoteRepositoryTests
{
    private static ApplicationDbContext CreateDbContext(string? databaseName = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SaveAsync_adds_a_new_note()
    {
        await using var dbContext = CreateDbContext();
        var repository = new NoteRepository(dbContext);

        await repository.SaveAsync(new Note
        {
            UserId = 1,
            TrainingModule = "understanding-development",
            Name = "key-concepts",
            Title = "Key concepts",
            Body = "My reflection",
        });

        var note = await dbContext.Notes.SingleAsync();
        Assert.Equal("My reflection", note.Body);
        Assert.Equal("key-concepts", note.Name);
    }

    [Fact]
    public async Task SaveAsync_updates_existing_note_without_duplicating()
    {
        await using var dbContext = CreateDbContext();
        var repository = new NoteRepository(dbContext);

        await repository.SaveAsync(new Note
        {
            UserId = 1,
            TrainingModule = "understanding-development",
            Name = "key-concepts",
            Body = "First draft",
        });

        var existing = await repository.GetByUserAndPageAsync(1, "understanding-development", "key-concepts");
        Assert.NotNull(existing);
        existing!.Body = "Edited reflection";
        await repository.SaveAsync(existing);

        var notes = await dbContext.Notes.ToListAsync();
        Assert.Single(notes);
        Assert.Equal("Edited reflection", notes[0].Body);
    }

    [Fact]
    public async Task GetByUserAndModuleAsync_filters_by_user_and_orders_by_updated_descending()
    {
        var databaseName = Guid.NewGuid().ToString();

        // A fresh context per save mirrors the scoped-per-request lifetime in the web app,
        // so the SaveChanges timestamp override only stamps the note being saved.
        await SaveNoteAsync(databaseName, userId: 1, name: "key-concepts", body: "Older");
        await Task.Delay(20);
        await SaveNoteAsync(databaseName, userId: 1, name: "applying-learning", body: "Newer");
        await SaveNoteAsync(databaseName, userId: 2, name: "key-concepts", body: "Other user");

        await using var dbContext = CreateDbContext(databaseName);
        var repository = new NoteRepository(dbContext);
        var notes = await repository.GetByUserAndModuleAsync(1, "understanding-development");

        Assert.Equal(2, notes.Count);
        Assert.Equal("Newer", notes[0].Body);
        Assert.Equal("Older", notes[1].Body);
        Assert.DoesNotContain(notes, n => n.Body == "Other user");
    }

    [Fact]
    public async Task GetByUserAndModulesAsync_filters_to_requested_modules_and_orders_by_updated_descending()
    {
        var databaseName = Guid.NewGuid().ToString();

        await SaveNoteAsync(databaseName, userId: 1, moduleName: "module-one", name: "older", body: "Older");
        await Task.Delay(20);
        await SaveNoteAsync(databaseName, userId: 1, moduleName: "module-two", name: "newer", body: "Newer");
        await SaveNoteAsync(databaseName, userId: 1, moduleName: "module-three", name: "ignored", body: "Ignored");
        await SaveNoteAsync(databaseName, userId: 2, moduleName: "module-one", name: "other-user", body: "Other user");

        await using var dbContext = CreateDbContext(databaseName);
        var repository = new NoteRepository(dbContext);

        var notes = await repository.GetByUserAndModulesAsync(1, ["module-one", "module-two"]);

        Assert.Equal(["Newer", "Older"], notes.Select(n => n.Body ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task SaveAsync_updates_detached_existing_note()
    {
        var databaseName = Guid.NewGuid().ToString();
        await SaveNoteAsync(databaseName, userId: 1, name: "key-concepts", body: "First draft");

        Note detached;
        await using (var readContext = CreateDbContext(databaseName))
        {
            detached = await readContext.Notes.AsNoTracking().SingleAsync();
        }

        detached.Body = "Edited while detached";

        await using var writeContext = CreateDbContext(databaseName);
        var repository = new NoteRepository(writeContext);
        await repository.SaveAsync(detached);

        Assert.Equal("Edited while detached", await writeContext.Notes.Select(n => n.Body).SingleAsync());
    }

    private static Task SaveNoteAsync(string databaseName, long userId, string name, string body) =>
        SaveNoteAsync(databaseName, userId, "understanding-development", name, body);

    private static async Task SaveNoteAsync(string databaseName, long userId, string moduleName, string name, string body)
    {
        await using var dbContext = CreateDbContext(databaseName);
        var repository = new NoteRepository(dbContext);
        await repository.SaveAsync(new Note
        {
            UserId = userId,
            TrainingModule = moduleName,
            Name = name,
            Body = body,
        });
    }
}
