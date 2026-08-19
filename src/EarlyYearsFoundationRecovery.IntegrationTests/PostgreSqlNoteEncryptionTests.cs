using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

[Trait("Category", "Database")]
public sealed class PostgreSqlNoteEncryptionTests(
    PostgreSqlSchemaFixture database) : IClassFixture<PostgreSqlSchemaFixture>
{
    [DatabaseFact]
    public async Task Rails_ciphertext_is_stored_in_text_and_reads_back_as_plaintext()
    {
        var connectionString = await database.CreateDatabaseAsync();
        var current = new RailsNoteBodyProtector(
            "postgres-note-current-test-key",
            "postgres-note-test-salt");

        await using (var writeContext = CreateContext(connectionString, current))
        {
            await writeContext.Database.EnsureCreatedAsync();
            var user = new User { Email = "note-encryption@example.test" };
            writeContext.Users.Add(user);
            await writeContext.SaveChangesAsync();

            writeContext.Notes.Add(new Note
            {
                UserId = user.Id,
                TrainingModule = "module-1",
                Name = "reflection",
                Body = "A private reflection\nwith two lines.",
            });
            writeContext.Notes.Add(new Note
            {
                UserId = user.Id,
                TrainingModule = "module-1",
                Name = "empty-reflection",
                Body = null,
            });
            await writeContext.SaveChangesAsync();
        }

        await using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var typeCommand = new NpgsqlCommand(
                "SELECT data_type FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'notes' AND column_name = 'body'",
                connection);
            Assert.Equal("text", await typeCommand.ExecuteScalarAsync());

            await using var bodyCommand = new NpgsqlCommand(
                "SELECT body FROM notes WHERE name = 'reflection'",
                connection);
            var rawBody = (string?)await bodyCommand.ExecuteScalarAsync();
            Assert.NotNull(rawBody);
            Assert.DoesNotContain("A private reflection", rawBody, StringComparison.Ordinal);
            Assert.StartsWith("{\"p\":", rawBody, StringComparison.Ordinal);

            await using var nullCommand = new NpgsqlCommand(
                "SELECT body FROM notes WHERE name = 'empty-reflection'",
                connection);
            Assert.Equal(DBNull.Value, await nullCommand.ExecuteScalarAsync());
        }

        await using var readContext = CreateContext(
            connectionString,
            new RailsNoteBodyProtector(
                "postgres-note-current-test-key",
                "postgres-note-test-salt"));
        var notes = await readContext.Notes
            .OrderBy(note => note.Name)
            .ToListAsync();

        Assert.Equal(2, notes.Count);
        Assert.Null(notes[0].Body);
        Assert.Equal("A private reflection\nwith two lines.", notes[1].Body);
    }

    private static ApplicationDbContext CreateContext(
        string connectionString,
        INoteBodyProtector protector) =>
        new(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention()
                .Options,
            protector);
}
