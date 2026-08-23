using System.Data;
using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public sealed record LegacyModuleProgressMigrationOptions(bool Apply = false, int BatchSize = 500)
{
    public const int MaximumBatchSize = 5_000;

    public void Validate()
    {
        if (BatchSize is < 1 or > MaximumBatchSize)
        {
            throw new ArgumentOutOfRangeException(nameof(BatchSize), $"Batch size must be between 1 and {MaximumBatchSize}.");
        }
    }
}

public sealed record LegacyModuleProgressMigrationReport(
    bool Applied,
    int EligibleUsers,
    int ExaminedEvents,
    int PlannedRows,
    int InsertedRows,
    int ConflictRows,
    int MalformedEvents,
    int UnmatchedEvents,
    int CompletionOnlyUsers,
    IReadOnlyDictionary<string, int> PlannedRowsByModule)
{
    public override string ToString() =>
        $"Mode: {(Applied ? "apply" : "dry-run")}. Eligible users: {EligibleUsers}; events examined: {ExaminedEvents}; " +
        $"rows planned: {PlannedRows}; rows inserted: {InsertedRows}; conflicts/skips: {ConflictRows}; " +
        $"malformed events: {MalformedEvents}; unmatched-module events: {UnmatchedEvents}; " +
        $"completion-only users (reported, not repaired): {CompletionOnlyUsers}; " +
        $"planned rows by module: {string.Join(", ", PlannedRowsByModule.OrderBy(item => item.Key).Select(item => $"{item.Key}={item.Value}"))}.";
}

/// <summary>
/// One-off cutover operation mirroring Rails v1.5.0 UserModuleProgress.migrate_from_events.
/// It is deliberately not part of the HTTP request path.
/// </summary>
public sealed class LegacyModuleProgressMigration(
    ApplicationDbContext dbContext,
    ITrainingContentProvider contentProvider)
{
    private static readonly HashSet<string> PageNames = ["module_content_page", "page_view"];

    public async Task<LegacyModuleProgressMigrationReport> RunAsync(
        LegacyModuleProgressMigrationOptions options,
        CancellationToken cancellationToken = default)
    {
        options.Validate();
        if (!dbContext.Database.IsNpgsql())
        {
            throw new InvalidOperationException("Legacy progress migration requires PostgreSQL.");
        }

        var liveModules = (await contentProvider.GetLiveModulesAsync(cancellationToken))
            .Select(module => module.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (liveModules.Count == 0)
        {
            throw new InvalidOperationException("Legacy progress migration requires a non-empty live-module snapshot.");
        }

        var connection = (NpgsqlConnection)dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var eligibleUsers = 0;
        var examinedEvents = 0;
        var plannedRows = 0;
        var insertedRows = 0;
        var conflictRows = 0;
        var malformedEvents = 0;
        var unmatchedEvents = 0;
        var plannedRowsByModule = liveModules.ToDictionary(module => module, _ => 0, StringComparer.Ordinal);
        long cursor = 0;

        while (true)
        {
            var userIds = await ReadEligibleUserBatchAsync(connection, cursor, options.BatchSize, cancellationToken);
            if (userIds.Count == 0)
            {
                break;
            }

            cursor = userIds[^1];
            eligibleUsers += userIds.Count;
            var events = await ReadEventsAsync(connection, userIds, cancellationToken);
            examinedEvents += events.Count;

            foreach (var userGroup in events.GroupBy(item => item.UserId))
            {
                malformedEvents += userGroup.Count(item => item.Time is null || item.ModuleName is null ||
                    (PageNames.Contains(item.Name) && string.IsNullOrWhiteSpace(item.PageName)));
                var derived = new List<DerivedProgress>();
                foreach (var module in liveModules)
                {
                    var progress = Derive(userGroup, module);
                    if (progress is null)
                    {
                        continue;
                    }

                    plannedRows++;
                    plannedRowsByModule[module]++;
                    derived.Add(progress);
                }
                if (options.Apply && derived.Count > 0)
                {
                    var inserted = await InsertUserProgressAsync(connection, userGroup.Key, derived, cancellationToken);
                    insertedRows += inserted;
                    conflictRows += derived.Count - inserted;
                }

                unmatchedEvents += userGroup.Count(item =>
                    item.ModuleName is not null && !liveModules.Contains(item.ModuleName));
            }
        }

        var completionOnlyUsers = await CountCompletionOnlyUsersAsync(connection, cancellationToken);
        return new(options.Apply, eligibleUsers, examinedEvents, plannedRows, insertedRows, conflictRows,
            malformedEvents, unmatchedEvents, completionOnlyUsers, plannedRowsByModule);
    }

    private static async Task<List<long>> ReadEligibleUserBatchAsync(
        NpgsqlConnection connection, long cursor, int batchSize, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.id
            FROM users u
            WHERE u.id > @cursor
              AND NOT EXISTS (SELECT 1 FROM user_module_progress p WHERE p.user_id = u.id)
              AND EXISTS (
                SELECT 1 FROM events e
                WHERE e.user_id = u.id
                  AND e.name IN ('module_start', 'module_content_page', 'page_view'))
            ORDER BY u.id
            LIMIT @batch_size
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("cursor", cursor);
        command.Parameters.AddWithValue("batch_size", batchSize);
        var result = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(reader.GetInt64(0));
        }
        return result;
    }

    private static async Task<List<LegacyEvent>> ReadEventsAsync(
        NpgsqlConnection connection, IReadOnlyList<long> userIds, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, user_id, name, time, properties::text
            FROM events
            WHERE user_id = ANY(@user_ids)
              AND name IN ('module_start', 'module_complete', 'module_content_page', 'page_view')
            ORDER BY user_id, time NULLS LAST, id
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_ids", userIds.ToArray());
        var result = new List<LegacyEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(reader.GetString(4)) ?? [];
            result.Add(new(
                reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                StringValue(properties, "training_module_id"), StringValue(properties, "id")));
        }
        return result;
    }

    private static DerivedProgress? Derive(IEnumerable<LegacyEvent> source, string moduleName)
    {
        var events = source.Where(item => string.Equals(item.ModuleName, moduleName, StringComparison.Ordinal)).ToList();
        var start = events.Where(item => item.Name == "module_start" && item.Time is not null)
            .Select(item => item.Time).FirstOrDefault();
        var complete = events.Where(item => item.Name == "module_complete" && item.Time is not null)
            .Select(item => item.Time).FirstOrDefault();
        var pages = events.Where(item => PageNames.Contains(item.Name) && item.Time is not null &&
                !string.IsNullOrWhiteSpace(item.PageName))
            .ToList();
        if (start is null && complete is null && pages.Count == 0)
        {
            return null;
        }

        var visited = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var page in pages)
        {
            visited.TryAdd(page.PageName!, VisitedPagesMapping.ToIso8601(page.Time!.Value));
        }
        var startedAt = start ?? pages.FirstOrDefault()?.Time;
        return new(events[0].UserId, moduleName, startedAt, complete,
            pages.LastOrDefault()?.PageName, visited);
    }

    private static async Task<int> InsertUserProgressAsync(
        NpgsqlConnection connection, long userId, IReadOnlyList<DerivedProgress> rows, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await using (var lockCommand = new NpgsqlCommand("SELECT id FROM users WHERE id = @user_id FOR UPDATE", connection, transaction))
        {
            lockCommand.Parameters.AddWithValue("user_id", userId);
            await lockCommand.ExecuteScalarAsync(cancellationToken);
        }
        await using (var gateCommand = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM user_module_progress WHERE user_id = @user_id)", connection, transaction))
        {
            gateCommand.Parameters.AddWithValue("user_id", userId);
            if ((bool)(await gateCommand.ExecuteScalarAsync(cancellationToken))!)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }
        }

        const string insertSql = """
            INSERT INTO user_module_progress
                (user_id, module_name, started_at, completed_at, last_page, visited_pages, created_at, updated_at)
            VALUES (@user_id, @module_name, @started_at, @completed_at, @last_page, @visited_pages::jsonb, NOW(), NOW())
            ON CONFLICT (user_id, module_name) DO NOTHING
            """;
        var inserted = 0;
        foreach (var progress in rows)
        {
            await using var command = new NpgsqlCommand(insertSql, connection, transaction);
            command.Parameters.AddWithValue("user_id", progress.UserId);
            command.Parameters.AddWithValue("module_name", progress.ModuleName);
            command.Parameters.AddWithValue("started_at", (object?)progress.StartedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("completed_at", (object?)progress.CompletedAt ?? DBNull.Value);
            command.Parameters.AddWithValue("last_page", (object?)progress.LastPage ?? DBNull.Value);
            command.Parameters.AddWithValue("visited_pages", JsonSerializer.Serialize(progress.VisitedPages));
            inserted += await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
        return inserted;
    }

    private static async Task<int> CountCompletionOnlyUsersAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(*)::integer FROM users u
            WHERE NOT EXISTS (SELECT 1 FROM user_module_progress p WHERE p.user_id = u.id)
              AND EXISTS (SELECT 1 FROM events e WHERE e.user_id = u.id AND e.name = 'module_complete')
              AND NOT EXISTS (SELECT 1 FROM events e WHERE e.user_id = u.id
                AND e.name IN ('module_start', 'module_content_page', 'page_view'))
            """;
        await using var command = new NpgsqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private static string? StringValue(Dictionary<string, JsonElement> values, string key) =>
        values.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record LegacyEvent(long Id, long UserId, string Name, DateTime? Time, string? ModuleName, string? PageName);
    private sealed record DerivedProgress(long UserId, string ModuleName, DateTime? StartedAt, DateTime? CompletedAt,
        string? LastPage, IReadOnlyDictionary<string, string> VisitedPages);
}
