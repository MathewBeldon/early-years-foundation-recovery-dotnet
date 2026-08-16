using System.Text.Json;
using EarlyYearsFoundationRecovery.Domain;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

[Trait("Category", "Database")]
public sealed class VisitedPagesJsonbCompatibilityTests(
    PostgreSqlSchemaFixture database) : IClassFixture<PostgreSqlSchemaFixture>
{
    private const string RailsTimestamp = "2024-06-01T09:30:00Z";

    [DatabaseFact]
    public async Task Reads_rails_iso8601_visited_pages_as_page_key_membership()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using var dbContext = CreateContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();
        var userId = await SeedUserAsync(dbContext);
        await InsertVisitedPagesAsync(
            connectionString,
            userId,
            """{"what-to-expect":"2024-06-01T09:30:00Z","1-1":"2024-06-01T09:31:00+01:00"}""");

        var progress = await dbContext.UserModuleProgress.AsNoTracking().SingleAsync();

        Assert.True(progress.VisitedPages.ContainsKey("what-to-expect"));
        Assert.True(progress.VisitedPages.ContainsKey("1-1"));
        Assert.Equal("2024-06-01T09:30:00Z", progress.VisitedPages["what-to-expect"]);
        Assert.Equal("2024-06-01T09:31:00+01:00", progress.VisitedPages["1-1"]);
    }

    [DatabaseFact]
    public async Task Reads_legacy_dotnet_boolean_visited_pages_as_page_key_membership()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using var dbContext = CreateContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();
        var userId = await SeedUserAsync(dbContext);
        await InsertVisitedPagesAsync(connectionString, userId, """{"key-concepts":true,"certificate":false}""");

        var progress = await dbContext.UserModuleProgress.AsNoTracking().SingleAsync();

        Assert.True(progress.VisitedPages.ContainsKey("key-concepts"));
        Assert.True(progress.VisitedPages.ContainsKey("certificate"));
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", progress.VisitedPages["key-concepts"]);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", progress.VisitedPages["certificate"]);
    }

    [DatabaseFact]
    public async Task Writes_rails_compatible_iso8601_object_and_preserves_existing_timestamps()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await using var dbContext = CreateContext(connectionString);
        await dbContext.Database.EnsureCreatedAsync();
        var userId = await SeedUserAsync(dbContext);
        await InsertVisitedPagesAsync(
            connectionString,
            userId,
            $$"""{"what-to-expect":"{{RailsTimestamp}}"}""");

        var progress = await dbContext.UserModuleProgress.SingleAsync();
        progress.VisitedPages = VisitedPagesMapping.Mark(
            progress.VisitedPages,
            "1-1",
            new DateTime(2026, 8, 16, 11, 0, 0, DateTimeKind.Utc));
        await dbContext.SaveChangesAsync();

        var raw = await ReadVisitedPagesAsync(connectionString);
        using var document = JsonDocument.Parse(raw);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("what-to-expect").ValueKind);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("1-1").ValueKind);
        Assert.Equal(RailsTimestamp, document.RootElement.GetProperty("what-to-expect").GetString());
        Assert.Equal("2026-08-16T11:00:00Z", document.RootElement.GetProperty("1-1").GetString());
        Assert.DoesNotContain("true", raw, StringComparison.Ordinal);
        Assert.DoesNotContain("false", raw, StringComparison.Ordinal);
    }

    private static ApplicationDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static async Task<long> SeedUserAsync(ApplicationDbContext dbContext)
    {
        var user = new User { Email = "visited-pages@example.test" };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static async Task InsertVisitedPagesAsync(string connectionString, long userId, string visitedPagesJson)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO user_module_progress (
                user_id, module_name, started_at, visited_pages, created_at, updated_at)
            VALUES (
                @user_id, 'alpha', NOW(), @visited_pages::jsonb, NOW(), NOW())
            """,
            connection);
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("visited_pages", visitedPagesJson);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadVisitedPagesAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT visited_pages::text FROM user_module_progress",
            connection);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
