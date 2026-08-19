using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public static class ContentfulWebhookParser
{
    public static string? TryGetContentTypeId(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (TryReadContentTypeId(root, out var contentTypeId))
        {
            return contentTypeId;
        }

        if (root.TryGetProperty("entity", out var entity) &&
            TryReadContentTypeId(entity, out contentTypeId))
        {
            return contentTypeId;
        }

        return null;
    }

    public static string? TryGetContentTypeId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            return TryGetContentTypeId(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadContentTypeId(JsonElement entry, out string? contentTypeId)
    {
        contentTypeId = null;

        if (entry.ValueKind != JsonValueKind.Object ||
            !entry.TryGetProperty("sys", out var sys) ||
            sys.ValueKind != JsonValueKind.Object ||
            !sys.TryGetProperty("contentType", out var contentType) ||
            contentType.ValueKind != JsonValueKind.Object ||
            !contentType.TryGetProperty("sys", out var contentTypeSys) ||
            contentTypeSys.ValueKind != JsonValueKind.Object ||
            !contentTypeSys.TryGetProperty("id", out var id))
        {
            return false;
        }

        contentTypeId = id.ValueKind == JsonValueKind.String ? id.GetString() : null;
        return !string.IsNullOrWhiteSpace(contentTypeId);
    }
}
