using System.Text.Json;
using EarlyYearsFoundationRecovery.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

internal static class JsonPropertyExtensions
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static PropertyBuilder<Dictionary<string, string>> AsVisitedPagesJsonb(
        this PropertyBuilder<Dictionary<string, string>> property)
    {
        property
            .HasConversion(
                value => VisitedPagesMapping.Serialize(value),
                value => VisitedPagesMapping.Parse(value, DateTime.UtcNow))
            .HasColumnType("jsonb");

        property.Metadata.SetValueComparer(CreateJsonValueComparer<Dictionary<string, string>>());
        return property;
    }

    internal static PropertyBuilder<List<string>> AsJsonbList(this PropertyBuilder<List<string>> property)
    {
        property
            .HasConversion(
                value => SerializeRailsAnswers(value),
                value => DeserializeRailsAnswers(value))
            .HasColumnType("jsonb");

        property.Metadata.SetValueComparer(CreateJsonValueComparer<List<string>>());
        return property;
    }

    internal static string SerializeRailsAnswers(List<string> answers)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            foreach (var answer in answers)
            {
                if (int.TryParse(
                        answer,
                        System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out var numericAnswer)
                    && numericAnswer > 0
                    && string.Equals(
                        answer,
                        numericAnswer.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        StringComparison.Ordinal))
                {
                    writer.WriteNumberValue(numericAnswer);
                }
                else
                {
                    writer.WriteStringValue(answer);
                }
            }
            writer.WriteEndArray();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    internal static List<string> DeserializeRailsAnswers(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return document.RootElement.EnumerateArray()
            .Select(answer => answer.ValueKind switch
            {
                JsonValueKind.Number => answer.GetRawText(),
                JsonValueKind.String => answer.GetString() ?? string.Empty,
                _ => string.Empty,
            })
            .Where(answer => answer.Length > 0)
            .ToList();
    }

    internal static PropertyBuilder<Dictionary<string, object?>> AsJsonbDictionary(
        this PropertyBuilder<Dictionary<string, object?>> property)
    {
        property
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => JsonSerializer.Deserialize<Dictionary<string, object?>>(value, JsonOptions)
                    ?? new Dictionary<string, object?>())
            .HasColumnType("jsonb");

        property.Metadata.SetValueComparer(CreateJsonValueComparer<Dictionary<string, object?>>());
        return property;
    }

    // Mutable reference types mapped through a value converter need a value comparer, otherwise EF
    // snapshots them by reference and silently misses in-place mutations. Comparing/cloning via JSON
    // keeps change tracking correct regardless of how callers update the collection.
    internal static ValueComparer<T> CreateJsonValueComparer<T>() => new(
        (left, right) => JsonSerializer.Serialize(left, JsonOptions) == JsonSerializer.Serialize(right, JsonOptions),
        value => value == null ? 0 : JsonSerializer.Serialize(value, JsonOptions).GetHashCode(StringComparison.Ordinal),
        value => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!);
}
