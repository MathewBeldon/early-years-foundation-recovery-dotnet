using System.Text.Json;
using EarlyYearsFoundationRecovery.Domain;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class VisitedPagesMappingTests
{
    private static readonly DateTime SynthesizedAt = new(2026, 8, 16, 10, 50, 0, DateTimeKind.Utc);

    [Fact]
    public void Parse_reads_rails_iso8601_timestamps_as_page_keys()
    {
        var visited = VisitedPagesMapping.Parse(
            """{"what-to-expect":"2024-06-01T09:30:00Z","1-1":"2024-06-01T09:31:00+01:00"}""",
            SynthesizedAt);

        Assert.Equal(2, visited.Count);
        Assert.True(visited.ContainsKey("what-to-expect"));
        Assert.True(visited.ContainsKey("1-1"));
        Assert.Equal("2024-06-01T09:30:00Z", visited["what-to-expect"]);
        Assert.Equal("2024-06-01T09:31:00+01:00", visited["1-1"]);
    }

    [Fact]
    public void Parse_reads_legacy_dotnet_booleans_as_page_keys()
    {
        var visited = VisitedPagesMapping.Parse(
            """{"key-concepts":true,"applying-learning":false}""",
            SynthesizedAt);

        Assert.True(visited.ContainsKey("key-concepts"));
        Assert.True(visited.ContainsKey("applying-learning"));
        Assert.Equal("2026-08-16T10:50:00Z", visited["key-concepts"]);
        Assert.Equal("2026-08-16T10:50:00Z", visited["applying-learning"]);
    }

    [Fact]
    public void Parse_reads_mixed_rails_and_dotnet_values()
    {
        var visited = VisitedPagesMapping.Parse(
            """{"rails-page":"2024-01-15T12:00:00Z","dotnet-page":true}""",
            SynthesizedAt);

        Assert.Equal("2024-01-15T12:00:00Z", visited["rails-page"]);
        Assert.Equal("2026-08-16T10:50:00Z", visited["dotnet-page"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    public void Parse_treats_missing_or_non_object_json_as_empty(string? json)
    {
        var visited = VisitedPagesMapping.Parse(json, SynthesizedAt);

        Assert.Empty(visited);
    }

    [Fact]
    public void Serialize_writes_iso8601_string_values_not_booleans()
    {
        var json = VisitedPagesMapping.Serialize(new Dictionary<string, string>
        {
            ["key-concepts"] = "2024-06-01T09:30:00Z",
        });

        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.Equal(JsonValueKind.String, document.RootElement.GetProperty("key-concepts").ValueKind);
        Assert.Equal("2024-06-01T09:30:00Z", document.RootElement.GetProperty("key-concepts").GetString());
        Assert.DoesNotContain(":true", json, StringComparison.Ordinal);
        Assert.DoesNotContain(":false", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_round_trips_rails_timestamp_strings_exactly()
    {
        const string original = "2024-06-01T09:30:00.123+01:00";
        var json = VisitedPagesMapping.Serialize(new Dictionary<string, string>
        {
            ["what-to-expect"] = original,
        });

        var visited = VisitedPagesMapping.Parse(json, SynthesizedAt);

        Assert.Equal(original, visited["what-to-expect"]);
    }

    [Fact]
    public void Mark_does_not_overwrite_an_existing_page_timestamp()
    {
        var existing = new Dictionary<string, string>
        {
            ["what-to-expect"] = "2024-01-01T00:00:00Z",
        };

        var updated = VisitedPagesMapping.Mark(existing, "what-to-expect", SynthesizedAt);

        Assert.Equal("2024-01-01T00:00:00Z", updated["what-to-expect"]);
        Assert.Equal("2024-01-01T00:00:00Z", existing["what-to-expect"]);
    }

    [Fact]
    public void Mark_adds_a_new_page_with_rails_iso8601_utc()
    {
        var updated = VisitedPagesMapping.Mark([], "what-to-expect", SynthesizedAt);

        Assert.Equal("2026-08-16T10:50:00Z", updated["what-to-expect"]);
    }
}
