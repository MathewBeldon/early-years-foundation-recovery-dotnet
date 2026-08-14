using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public sealed record RailsSchemaPreflightResult(bool IsCompatible, string Message);

public static class RailsSchemaCompatibility
{
    public const string RequiredRailsVersion = "20260529104000";
    private static readonly string[] RequiredTables =
    [
        "users", "user_module_progress", "assessments", "responses", "notes", "visits", "events",
        "mail_events", "confidence_check_progress", "releases", "module_releases", "que_jobs", "schema_migrations",
    ];

    private static readonly string[] BaselineMigrationIds = ["20260214120000_RailsBaseline"];

    public static async Task<RailsSchemaPreflightResult> PreflightAsync(
        ApplicationDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        await OpenAsync(connection, cancellationToken);
        var missing = new List<string>();
        foreach (var table in RequiredTables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@table) IS NOT NULL";
            AddParameter(command, "table", $"public.{table}");
            if (await command.ExecuteScalarAsync(cancellationToken) is not true)
            {
                missing.Add(table);
            }
        }

        if (missing.Count > 0)
        {
            return new(false, $"Database is not Rails-shaped. Missing required tables: {string.Join(", ", missing)}.");
        }

        await using var versionCommand = connection.CreateCommand();
        versionCommand.CommandText = "SELECT MAX(version) FROM schema_migrations";
        var version = Convert.ToString(await versionCommand.ExecuteScalarAsync(cancellationToken));
        if (string.CompareOrdinal(version, RequiredRailsVersion) < 0)
        {
            return new(false, $"Rails schema version {version ?? "<none>"} is older than required {RequiredRailsVersion}.");
        }

        return new(true, $"Rails schema {version} contains all {RequiredTables.Length} required compatibility tables.");
    }

    public static async Task BaselineAndMigrateAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var result = await PreflightAsync(dbContext, cancellationToken);
        if (!result.IsCompatible)
        {
            throw new InvalidOperationException(result.Message);
        }

        await dbContext.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
                migration_id character varying(150) NOT NULL PRIMARY KEY,
                product_version character varying(32) NOT NULL
            )
            """, cancellationToken);
        foreach (var migrationId in BaselineMigrationIds)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO "__EFMigrationsHistory" (migration_id, product_version)
                VALUES ({{migrationId}}, '10.0.8')
                ON CONFLICT (migration_id) DO NOTHING
                """, cancellationToken);
        }
        await dbContext.Database.MigrateAsync(cancellationToken);
    }

    private static async Task OpenAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
