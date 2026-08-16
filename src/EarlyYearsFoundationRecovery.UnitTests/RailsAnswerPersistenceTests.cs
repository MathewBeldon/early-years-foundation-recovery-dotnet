using EarlyYearsFoundationRecovery.Infrastructure.Persistence;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class RailsAnswerPersistenceTests
{
    [Fact]
    public void Numeric_answer_ids_are_json_numbers_and_legacy_text_remains_compatible()
    {
        var json = JsonPropertyExtensions.SerializeRailsAnswers(["1", "legacy object answer"]);

        Assert.Equal("[1,\"legacy object answer\"]", json);
        Assert.Equal(["1", "legacy object answer"], JsonPropertyExtensions.DeserializeRailsAnswers(json));
    }
}
