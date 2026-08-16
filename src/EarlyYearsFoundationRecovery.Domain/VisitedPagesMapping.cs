using System.Globalization;
using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Domain;

/// <summary>
/// Shared-schema mapping for <c>user_module_progress.visited_pages</c>.
/// Rails v1.5.0 (ac546721) stores a jsonb object whose values are ISO8601
/// timestamps and treats visit as key membership
/// (<c>visited_pages&.key?(page_name)</c>), writing with
/// <c>visited_pages[page_name] ||= Time.zone.now.iso8601</c>.
/// Existing .NET rows may still contain boolean values.
/// </summary>
public static class VisitedPagesMapping
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Dictionary<string, string> Parse(string? json, DateTime utcNow)
    {
        var visited = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
        {
            return visited;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return visited;
        }

        var synthesized = ToIso8601(utcNow);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            visited[property.Name] = ReadValue(property.Value, synthesized);
        }

        return visited;
    }

    public static string Serialize(Dictionary<string, string>? visitedPages) =>
        JsonSerializer.Serialize(visitedPages ?? new Dictionary<string, string>(StringComparer.Ordinal), JsonOptions);

    public static Dictionary<string, string> Mark(
        Dictionary<string, string> visitedPages,
        string pageName,
        DateTime utcNow)
    {
        var updated = new Dictionary<string, string>(visitedPages, StringComparer.Ordinal);
        updated.TryAdd(pageName, ToIso8601(utcNow));
        return updated;
    }

    public static string ToIso8601(DateTime utcNow) =>
        utcNow.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string ReadValue(JsonElement value, string synthesizedTimestamp) =>
        value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } timestamp
            ? timestamp
            : synthesizedTimestamp;
}
