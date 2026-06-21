using System.Security.Cryptography;
using System.Text;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[ApiController]
public sealed class ContentfulWebhookController(
    IOptions<ContentfulOptions> options,
    IContentfulContentCache contentCache,
    ILogger<ContentfulWebhookController> logger) : ControllerBase
{
    public const string WebhookSecretHeader = "X-Contentful-Webhook-Secret";
    public const string LegacyBotHeader = "BOT";

    [HttpPost("contentful/webhook")]
    [HttpPost("change")]
    [HttpPost("release")]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.IsConfigured)
        {
            return NotFound(new { status = "contentful not configured" });
        }

        if (string.IsNullOrWhiteSpace(settings.WebhookSecret))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "contentful webhook secret not configured",
            });
        }

        if (!IsAuthorized(settings.WebhookSecret))
        {
            return Unauthorized(new { status = "invalid webhook secret" });
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

        return Ok(new { status = "content cache cleared" });
    }

    private bool IsAuthorized(string expectedSecret)
    {
        if (Request.Headers.TryGetValue(WebhookSecretHeader, out var secretHeader) &&
            SecretsMatch(secretHeader.FirstOrDefault(), expectedSecret))
        {
            return true;
        }

        if (Request.Headers.TryGetValue(LegacyBotHeader, out var botHeader) &&
            SecretsMatch(botHeader.FirstOrDefault(), expectedSecret))
        {
            return true;
        }

        return false;
    }

    private static bool SecretsMatch(string? provided, string expected)
    {
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}
