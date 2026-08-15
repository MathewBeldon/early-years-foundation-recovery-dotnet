using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Domain.Entities;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
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
        var topic = Request.Headers["X-Contentful-Topic"].FirstOrDefault();
        var contentTypeId = ContentfulWebhookParser.TryGetContentTypeId(payload);

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

        if (Request.Path.Equals("/release", StringComparison.OrdinalIgnoreCase) ||
            Request.Path.Equals("/change", StringComparison.OrdinalIgnoreCase))
        {
            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("sys", out var sys) ||
                !sys.TryGetProperty("id", out var id))
            {
                return BadRequest(new { status = "release payload missing sys.id" });
            }
            var timeProperty = Request.Path.Equals("/release", StringComparison.OrdinalIgnoreCase)
                ? "completedAt"
                : "updatedAt";
            if (!sys.TryGetProperty(timeProperty, out var time) || !time.TryGetDateTime(out var timestamp))
            {
                return BadRequest(new { status = $"release payload missing sys.{timeProperty}" });
            }
            dbContext.Releases.Add(new Release
            {
                Name = id.GetString() ?? string.Empty,
                Time = timestamp.ToUniversalTime(),
                Properties = JsonSerializer.Deserialize<Dictionary<string, object?>>(payload) ?? [],
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new
            {
                status = Request.Path.Equals("/release", StringComparison.OrdinalIgnoreCase)
                    ? "content release received"
                    : "content change received",
            });
        }

        return Ok(new { status = "content cache cleared" });
    }

}
