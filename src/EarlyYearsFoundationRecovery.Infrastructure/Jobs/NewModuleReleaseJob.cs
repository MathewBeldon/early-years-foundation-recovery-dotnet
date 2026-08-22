using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Notify;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

internal sealed class NewModuleReleaseJob(
    ApplicationDbContext db,
    ITrainingContentProvider content,
    IOptions<InfrastructureOptions> options,
    TimeProvider timeProvider)
{
    public const string JobType = "new_module_release";

    public async Task RunAsync(string payload, CancellationToken cancellationToken = default)
    {
        var releaseId = JsonSerializer.Deserialize<ReleasePayload>(payload)?.ReleaseId
            ?? throw new InvalidOperationException("new_module_release payload has no releaseId.");
        var release = await db.Releases.SingleOrDefaultAsync(x => x.Id == releaseId, cancellationToken)
            ?? throw new InvalidOperationException($"Release {releaseId} does not exist.");
        var module = (await content.GetLiveModulesAsync(cancellationToken))
            .OrderBy(x => x.Position).LastOrDefault()
            ?? throw new InvalidOperationException("Contentful has no live module to reserve.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var insertedId = await InsertReservationAsync(release, module, cancellationToken);
        if (insertedId is null)
        {
            var matches = await db.ModuleReleases
                .Where(x => x.Name == module.Name || x.ModulePosition == module.Position)
                .ToListAsync(cancellationToken);
            if (matches.Count == 1 && matches[0].Name == module.Name && matches[0].ModulePosition == module.Position)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            throw new InvalidOperationException(
                $"Module release integrity conflict for name '{module.Name}' and position {module.Position}.");
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await ScheduleDeliveriesAsync(insertedId.Value, module, now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ScheduleDeliveriesAsync(
        long moduleReleaseId,
        TrainingModuleContent module,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            WITH scheduled AS (
                INSERT INTO dotnet_new_module_notification_deliveries
                    (module_release_id, user_id, template_id, module_position, module_title,
                     module_criteria, public_url, status, created_at, updated_at)
                SELECT @module_release_id, users.id, @template, @module_position, @module_title,
                       @module_criteria, @public_url, 'pending', @now, @now
                FROM users
                WHERE users.closed_at IS NULL
                  AND users.training_emails IS NOT FALSE
                  AND NOT EXISTS (
                      SELECT 1
                      FROM mail_events
                      WHERE mail_events.user_id = users.id
                        AND mail_events.template = @template
                        AND mail_events.personalisation @> CAST(@personalisation AS jsonb)
                  )
                ON CONFLICT (module_release_id, user_id, template_id) DO NOTHING
                RETURNING id
            )
            INSERT INTO background_jobs
                (job_type, payload, status, attempts, max_attempts, run_at,
                 locked_at, locked_by, completed_at, last_error, created_at, updated_at)
            SELECT @job_type, jsonb_build_object('deliveryId', scheduled.id), 'queued', 0, 5, @now,
                   NULL, NULL, NULL, NULL, @now, @now
            FROM scheduled
            """;
        AddParameter(command, "module_release_id", moduleReleaseId);
        AddParameter(command, "template", NotifyTemplateIds.NewModule);
        AddParameter(command, "module_position", module.Position);
        AddParameter(command, "module_title", module.Title);
        AddParameter(command, "module_criteria", module.Criteria);
        AddParameter(command, "public_url", options.Value.PublicBaseUrl.TrimEnd('/') + "/");
        AddParameter(command, "personalisation", JsonSerializer.Serialize(new { mod_number = module.Position }));
        AddParameter(command, "job_type", NewModuleNotificationDeliveryJob.JobType);
        AddParameter(command, "now", now);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<long?> InsertReservationAsync(
        Release release,
        TrainingModuleContent module,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            INSERT INTO module_releases
                (release_id, module_position, name, first_published_at, created_at, updated_at)
            VALUES (@release_id, @module_position, @name, @first_published_at, @created_at, @updated_at)
            ON CONFLICT DO NOTHING
            RETURNING id
            """;
        AddParameter(command, "release_id", release.Id);
        AddParameter(command, "module_position", module.Position);
        AddParameter(command, "name", module.Name);
        AddParameter(command, "first_published_at", release.Time);
        AddParameter(command, "created_at", now);
        AddParameter(command, "updated_at", now);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null or DBNull ? null : Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private sealed record ReleasePayload([property: JsonPropertyName("releaseId")] long ReleaseId);
}
