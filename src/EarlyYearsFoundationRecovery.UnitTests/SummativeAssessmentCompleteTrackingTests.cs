using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class SummativeAssessmentCompleteTrackingTests
{
    private const string ModuleName = "module-1";

    private static readonly TrainingPageContent ResultsPage = new(
        "assessment-results",
        "assessment_results",
        "Results",
        string.Empty,
        [],
        null,
        null,
        ContentId: "results-content-id");

    private static readonly TrainingModuleContent Module = new(
        ModuleName,
        "Module one",
        string.Empty,
        string.Empty,
        string.Empty,
        1,
        1,
        true,
        [ResultsPage],
        ContentId: "module-content-id");

    [Fact]
    public void Passed_graded_assessment_records_rails_type_module_score_and_success()
    {
        var assessment = Graded(score: 80, passed: true);

        Assert.True(SummativeAssessmentCompleteTracking.ShouldRecord(assessment, ModuleName, []));

        var properties = SummativeAssessmentCompleteTracking.CreateProperties(Module, ResultsPage, assessment);
        Assert.Equal("summative_assessment", properties["type"]);
        Assert.Equal(ModuleName, properties["training_module_id"]);
        Assert.Equal("assessment-results", properties["id"]);
        Assert.Equal("results-content-id", properties["uid"]);
        Assert.Equal("module-content-id", properties["mod_uid"]);
        Assert.Equal(80f, properties["score"]);
        Assert.Equal(true, properties["success"]);
    }

    [Fact]
    public void Failed_graded_assessment_records_success_false()
    {
        var assessment = Graded(score: 40, passed: false);

        Assert.True(SummativeAssessmentCompleteTracking.ShouldRecord(assessment, ModuleName, []));
        var properties = SummativeAssessmentCompleteTracking.CreateProperties(Module, ResultsPage, assessment);
        Assert.Equal(false, properties["success"]);
        Assert.Equal(40f, properties["score"]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    public void Ungraded_assessment_does_not_record(float? score, bool? passed)
    {
        Assessment? assessment = score is null && passed is null
            ? null
            : new Assessment { TrainingModule = ModuleName, Score = score, Passed = passed };

        Assert.False(SummativeAssessmentCompleteTracking.ShouldRecord(assessment, ModuleName, []));
    }

    [Fact]
    public void Repeated_invocation_skips_when_the_same_success_value_is_already_stored()
    {
        // Rails v1.5.0 ac546721 Training::AssessmentsController#track_events only
        // suppresses once a success:true event exists, so every failed results GET
        // writes another event. .NET also treats an existing success:false event
        // for the same training_module_id as already recorded.
        var failed = Graded(score: 50, passed: false);
        var existingFailure = EventFor(ModuleName, success: false);

        Assert.False(SummativeAssessmentCompleteTracking.ShouldRecord(failed, ModuleName, [existingFailure]));
        Assert.False(SummativeAssessmentCompleteTracking.ShouldRecord(
            Graded(score: 90, passed: true),
            ModuleName,
            [EventFor(ModuleName, success: true)]));
    }

    [Fact]
    public void Existing_failure_does_not_block_a_later_pass_for_the_same_module()
    {
        Assert.True(SummativeAssessmentCompleteTracking.ShouldRecord(
            Graded(score: 80, passed: true),
            ModuleName,
            [EventFor(ModuleName, success: false)]));
    }

    [Fact]
    public void Existing_pass_blocks_a_later_failure_matching_rails_pass_once_semantics()
    {
        Assert.False(SummativeAssessmentCompleteTracking.ShouldRecord(
            Graded(score: 20, passed: false),
            ModuleName,
            [EventFor(ModuleName, success: true)]));
    }

    [Fact]
    public void Events_for_another_module_do_not_suppress_tracking()
    {
        Assert.True(SummativeAssessmentCompleteTracking.ShouldRecord(
            Graded(score: 70, passed: true),
            ModuleName,
            [EventFor("module-2", success: true)]));
    }

    private static Assessment Graded(float score, bool passed) =>
        new()
        {
            TrainingModule = ModuleName,
            Score = score,
            Passed = passed,
            CompletedAt = DateTime.UtcNow,
        };

    private static Event EventFor(string moduleName, bool success) =>
        new()
        {
            Name = SummativeAssessmentCompleteTracking.EventName,
            UserId = 1,
            Properties = new Dictionary<string, object?>
            {
                ["training_module_id"] = moduleName,
                ["success"] = success,
            },
        };
}
