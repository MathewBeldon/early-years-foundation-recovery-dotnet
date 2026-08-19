using System.Diagnostics;
using DotNet.Testcontainers.Configurations;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit.Abstractions;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

[Trait("Category", "Database")]
public sealed class PostgreSqlSchemaCompatibilityTests(
    PostgreSqlSchemaFixture database,
    ITestOutputHelper output) : IClassFixture<PostgreSqlSchemaFixture>
{
    private const string PreviousRailsVersion = "20260214120000";
    private const string RequiredRailsVersion = "20260529104000";
    private const string RailsOwnedCountry = "Rails-owned country sentinel";

    [DatabaseFact]
    public async Task Schema_preflight_refuses_a_non_Rails_shaped_database_without_changing_it()
    {
        var connectionString = await database.CreateDatabaseAsync();

        var result = await RunAppAsync(connectionString, "--schema-preflight");

        output.WriteLine($"Non-Rails-shaped preflight exit code: {result.ExitCode}");
        Assert.True(result.ExitCode == 2, $"Expected exit code 2, but observed {result.ExitCode}. Output: {result.AllOutput}");
        Assert.Contains("Missing required tables", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("users", result.StandardOutput, StringComparison.Ordinal);
        Assert.False(await TableExistsAsync(connectionString, "__EFMigrationsHistory"));
    }

    [DatabaseFact]
    public async Task Schema_preflight_fails_closed_when_note_encryption_config_is_missing()
    {
        var connectionString = await database.CreateDatabaseAsync();

        var result = await RunAppAsync(connectionString, "--schema-preflight", includeNoteEncryption: false);

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("NoteEncryption:PrimaryKey is missing or blank", result.AllOutput, StringComparison.Ordinal);
        Assert.False(await TableExistsAsync(connectionString, "__EFMigrationsHistory"));
    }

    [DatabaseFact]
    public async Task Schema_preflight_refuses_a_stale_Rails_schema_without_changing_it()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await ApplyShapeAsync(connectionString, PreviousRailsVersion);

        var result = await RunAppAsync(connectionString, "--schema-preflight");

        output.WriteLine($"Stale Rails schema preflight exit code: {result.ExitCode}");
        Assert.True(result.ExitCode == 2, $"Expected exit code 2, but observed {result.ExitCode}. Output: {result.AllOutput}");
        Assert.Contains($"older than required {RequiredRailsVersion}", result.StandardOutput, StringComparison.Ordinal);
        Assert.False(await TableExistsAsync(connectionString, "__EFMigrationsHistory"));
    }

    [DatabaseFact]
    public async Task Migrate_adds_DotNet_schema_to_a_compatible_database_without_replacing_Rails_ownership()
    {
        var connectionString = await database.CreateDatabaseAsync();
        await ApplyShapeAsync(connectionString, RequiredRailsVersion);

        var result = await RunAppAsync(connectionString, "--migrate");

        Assert.True(result.ExitCode == 0, $"Expected exit code 0, but observed {result.ExitCode}. Output: {result.AllOutput}");
        Assert.True(await MigrationExistsAsync(connectionString, "20260214120000_RailsBaseline"), "The Rails baseline was not recorded.");
        Assert.True(await RelationExistsAsync(connectionString, "public.background_jobs"), "background_jobs was not created.");
        Assert.True(await ColumnExistsAsync(connectionString, "mail_events", "notification_id"), "mail_events.notification_id was not created.");
        Assert.True(
            string.Equals(await CountryAsync(connectionString), RailsOwnedCountry, StringComparison.Ordinal),
            "The Rails-owned users.country value did not survive the .NET migration unchanged.");
    }

    private static async Task ApplyShapeAsync(string connectionString, string railsVersion)
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "preflight-shape.sql");
        var sql = await File.ReadAllTextAsync(fixturePath);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using (var shape = new NpgsqlCommand(sql, connection))
        {
            await shape.ExecuteNonQueryAsync();
        }

        await using var version = new NpgsqlCommand("INSERT INTO schema_migrations (version) VALUES (@version)", connection);
        version.Parameters.AddWithValue("version", railsVersion);
        await version.ExecuteNonQueryAsync();
    }

    private static async Task<AppProcessResult> RunAppAsync(
        string connectionString,
        string argument,
        bool includeNoteEncryption = true)
    {
        var assemblyPath = typeof(Program).Assembly.Location;
        var executableName = OperatingSystem.IsWindows()
            ? $"{Path.GetFileNameWithoutExtension(assemblyPath)}.exe"
            : Path.GetFileNameWithoutExtension(assemblyPath);
        var executablePath = Path.Combine(Path.GetDirectoryName(assemblyPath)!, executableName);
        Assert.True(File.Exists(executablePath), $"Built application executable was not found at {executablePath}.");

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = argument,
                WorkingDirectory = Path.GetDirectoryName(executablePath)!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["ConnectionStrings__DefaultConnection"] = connectionString;
        if (includeNoteEncryption)
        {
            // Synthetic process-only credentials keep schema tests deterministic;
            // they are not app configuration and must never be deployed.
            process.StartInfo.Environment["NoteEncryption__PrimaryKey"] = "schema-preflight-test-primary-key";
            process.StartInfo.Environment["NoteEncryption__KeyDerivationSalt"] = "schema-preflight-test-salt";
        }

        Assert.True(process.Start(), $"Failed to start {executablePath} {argument}.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new(process.ExitCode, await standardOutput, await standardError);
    }

    private static async Task<bool> RelationExistsAsync(string connectionString, string relation)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT to_regclass(@relation) IS NOT NULL", connection);
        command.Parameters.AddWithValue("relation", relation);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> MigrationExistsAsync(string connectionString, string migrationId)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var quotedHistoryTable = '"' + "__EFMigrationsHistory" + '"';
        await using var command = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM {quotedHistoryTable} WHERE migration_id = @migration_id)", connection);
        command.Parameters.AddWithValue("migration_id", migrationId);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> TableExistsAsync(string connectionString, string table)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @table)",
            connection);
        command.Parameters.AddWithValue("table", table);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<bool> ColumnExistsAsync(string connectionString, string table, string column)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table AND column_name = @column)",
            connection);
        command.Parameters.AddWithValue("table", table);
        command.Parameters.AddWithValue("column", column);
        return (bool)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<string?> CountryAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT country FROM users WHERE id = 1", connection);
        return (string?)await command.ExecuteScalarAsync();
    }

    private sealed record AppProcessResult(int ExitCode, string StandardOutput, string StandardError)
    {
        public string AllOutput => $"stdout: {StandardOutput}{Environment.NewLine}stderr: {StandardError}";
    }
}

public sealed class PostgreSqlSchemaFixture : IAsyncLifetime
{
    private const string OptOutVariable = "DATABASE_TESTS_OPTIONAL";
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:15-alpine")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private string? _adminConnectionString;
    private Exception? _startupFailure;

    public async Task InitializeAsync()
    {
        if (DatabaseRuntime.HasExternalConnection)
        {
            _adminConnectionString = DatabaseRuntime.ExternalConnectionString;
            return;
        }

        try
        {
            await _container.StartAsync();
            _adminConnectionString = _container.GetConnectionString();
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
    }

    public async Task<string> CreateDatabaseAsync()
    {
        RequireRuntime();
        var databaseName = $"schema_compatibility_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(_adminConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE {databaseName}", connection);
        await command.ExecuteNonQueryAsync();

        var connectionString = new NpgsqlConnectionStringBuilder(_adminConnectionString)
        {
            Database = databaseName,
        };
        return connectionString.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (_startupFailure is null && !DatabaseRuntime.HasExternalConnection)
        {
            await _container.DisposeAsync();
        }
    }

    private void RequireRuntime()
    {
        if (_startupFailure is null)
        {
            return;
        }

        var detail = $"{_startupFailure.GetType().Name}: {_startupFailure.Message}";
        Assert.Fail(
            "The database test suite requires Docker or Podman with its Docker-compatible API reachable. " +
            $"Start the container runtime, or set {OptOutVariable}=1 to report an explicit skip. {detail}");
    }
}

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (DatabaseRuntime.HasExternalConnection)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DatabaseRuntime.OptOutVariable)))
        {
            return;
        }

        if (!DatabaseRuntime.IsReachable)
        {
            Skip = $"{DatabaseRuntime.OptOutVariable} is set and the container runtime is unreachable. " +
                $"Database compatibility was not tested. {DatabaseRuntime.FailureDetail}";
        }
    }
}

public static class DatabaseRuntime
{
    public const string OptOutVariable = "DATABASE_TESTS_OPTIONAL";
    public const string ExternalConnectionVariable = "POSTGRES_TEST_CONNECTION";
    private static readonly Lazy<(bool IsReachable, string FailureDetail)> Probe = new(ProbeRuntime);

    public static string? ExternalConnectionString =>
        Environment.GetEnvironmentVariable(ExternalConnectionVariable);

    public static bool HasExternalConnection =>
        !string.IsNullOrWhiteSpace(ExternalConnectionString);

    public static bool IsReachable => Probe.Value.IsReachable;

    public static string FailureDetail => Probe.Value.FailureDetail;

    private static (bool IsReachable, string FailureDetail) ProbeRuntime()
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var client = TestcontainersSettings.OS.DockerEndpointAuthConfig
                .GetDockerClientBuilder(Guid.NewGuid())
                .Build();
            client.System.PingAsync(timeout.Token).GetAwaiter().GetResult();
            return (true, string.Empty);
        }
        catch (Exception exception)
        {
            return (false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
