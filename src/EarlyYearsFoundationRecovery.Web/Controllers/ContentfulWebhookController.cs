using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Domain.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using EarlyYearsFoundationRecovery.Web.Authentication;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[ApiController]
public sealed class ContentfulWebhookController(
    IOptions<ContentfulOptions> options,
    IContentfulContentCache contentCache,
    ApplicationDbContext dbContext,
    ILogger<ContentfulWebhookController> logger,
    BotAuthenticationFailureTracker failureTracker) : ControllerBase
{
    private const string AuthenticationScope = "contentful-webhook";
    private const string InvalidPayloadTitle = "Invalid Contentful webhook payload";
    private const string InvalidJsonObjectDetail = "Request body must contain a valid JSON object.";
    private const string MissingSysDetail = "Payload must contain a sys object.";
    private const string InvalidIdDetail = "Payload must contain a non-blank string sys.id.";
    public const string BotHeader = "BOT";

    [HttpPost("change")]
    [HttpPost("release")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.WebhookSecret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "contentful webhook secret not configured",
            });
        }

        var authenticationFailure = this.EnforceBotAuthentication(
            failureTracker,
            AuthenticationScope,
            BotAuthentication.SecretsMatch(Request.Headers[BotHeader].FirstOrDefault(), settings.WebhookSecret));
        if (authenticationFailure is not null)
        {
            return authenticationFailure;
        }

        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return InvalidPayload(InvalidJsonObjectDetail, "invalid-json");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvalidPayload(InvalidJsonObjectDetail, "non-object-root");
            }

            if (!root.TryGetProperty("sys", out var sys) || sys.ValueKind != JsonValueKind.Object)
            {
                return InvalidPayload(MissingSysDetail, "invalid-sys");
            }

            if (!sys.TryGetProperty("id", out var id) ||
                id.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(id.GetString()))
            {
                return InvalidPayload(InvalidIdDetail, "invalid-sys-id");
            }

            var isRelease = Request.Path.Equals("/release", StringComparison.OrdinalIgnoreCase);
            var timeProperty = isRelease ? "completedAt" : "updatedAt";
            var invalidTimeDetail = $"Payload must contain a valid sys.{timeProperty} timestamp.";
            if (!sys.TryGetProperty(timeProperty, out var time) ||
                time.ValueKind != JsonValueKind.String ||
                !time.TryGetDateTime(out var timestamp))
            {
                return InvalidPayload(invalidTimeDetail, $"invalid-sys-{timeProperty}");
            }

            var release = new Release
            {
                Name = id.GetString()!,
                Time = timestamp.ToUniversalTime(),
                Properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(root) ?? [],
            };
            dbContext.Releases.Add(release);

            // PostgreSQL supplies Release.Id, so the job payload can only be built after
            // the release insert. Keep both flushes inside one explicit transaction: a
            // job insert failure rolls the release back. Testing uses EF's non-relational
            // provider, where generated IDs are assigned by the first flush.
            await using var transaction = dbContext.Database.IsRelational()
                ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;
            await dbContext.SaveChangesAsync(cancellationToken);

            dbContext.BackgroundJobs.Add(new BackgroundJob
            {
                JobType = isRelease ? "new_module_release" : "content_check",
                Payload = isRelease
                    ? JsonSerializer.Serialize(new { releaseId = release.Id })
                    : "{}",
                RunAt = DateTime.UtcNow,
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // Cache invalidation follows the durable commit so failed persistence does
            // not evict valid cached content.

            var topic = Request.Headers["X-Contentful-Topic"].FirstOrDefault();
            var contentTypeId = ContentfulWebhookParser.TryGetContentTypeId(root);
            if (string.IsNullOrWhiteSpace(contentTypeId))
            {
                contentCache.InvalidateAll();
                logger.LogInformation(
                    "Contentful webhook ({Topic}): cleared all content caches (no content type in payload).",
                    topic);
            }
            else
            {
                contentCache.InvalidateForContentType(contentTypeId);
                logger.LogInformation(
                    "Contentful webhook ({Topic}): cleared cache for content type {ContentTypeId}.",
                    topic,
                    contentTypeId);
            }
            return Ok(new
            {
                status = isRelease
                    ? "content release received"
                    : "content change received",
            });
        }
    }

    private ObjectResult InvalidPayload(string detail, string failure)
    {
        logger.LogWarning(
            "Rejected Contentful webhook on {Path}: {Failure}.",
            Request.Path,
            failure);
        return Problem(
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            title: InvalidPayloadTitle);
    }
}
