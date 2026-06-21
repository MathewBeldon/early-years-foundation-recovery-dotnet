using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

internal static class JsonPropertyExtensions
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static PropertyBuilder<Dictionary<string, bool>> AsJsonbDictionary(
        this PropertyBuilder<Dictionary<string, bool>> property)
    {
        property
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => JsonSerializer.Deserialize<Dictionary<string, bool>>(value, JsonOptions)
                    ?? new Dictionary<string, bool>())
            .HasColumnType("jsonb");

        property.Metadata.SetValueComparer(CreateJsonValueComparer<Dictionary<string, bool>>());
        return property;
    }

    internal static PropertyBuilder<List<string>> AsJsonbList(this PropertyBuilder<List<string>> property)
    {
        property
            .HasConversion(
                value => JsonSerializer.Serialize(value, JsonOptions),
                value => JsonSerializer.Deserialize<List<string>>(value, JsonOptions)
                    ?? new List<string>())
            .HasColumnType("jsonb");

        property.Metadata.SetValueComparer(CreateJsonValueComparer<List<string>>());
        return property;
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
