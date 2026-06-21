using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public static class ContentfulWebhookParser
{
    public static string? TryGetContentTypeId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (TryReadContentTypeId(root, out var contentTypeId))
            {
                return contentTypeId;
            }

            if (root.TryGetProperty("entity", out var entity) &&
                TryReadContentTypeId(entity, out contentTypeId))
            {
                return contentTypeId;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static bool TryReadContentTypeId(JsonElement entry, out string? contentTypeId)
    {
        contentTypeId = null;

        if (!entry.TryGetProperty("sys", out var sys) ||
            !sys.TryGetProperty("contentType", out var contentType) ||
            !contentType.TryGetProperty("sys", out var contentTypeSys) ||
            !contentTypeSys.TryGetProperty("id", out var id))
        {
            return false;
        }

        contentTypeId = id.GetString();
        return !string.IsNullOrWhiteSpace(contentTypeId);
    }
}
