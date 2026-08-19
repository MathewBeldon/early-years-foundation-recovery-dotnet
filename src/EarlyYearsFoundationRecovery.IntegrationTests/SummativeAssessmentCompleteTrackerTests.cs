using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using EarlyYearsFoundationRecovery.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class SummativeAssessmentCompleteTrackerTests
{
    private const string ModuleName = "module-1";
    private const string ResultsPath = "/modules/module-1/assessment-result/assessment-results";

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
    public async Task Passed_assessment_writes_summative_assessment_complete_with_rails_properties()
    {
        await using var db = CreateDb();
        var tracker = CreateTracker(db);
        var context = CreateHttpContext(ResultsPath);

        await tracker.TrackAsync(context, 42, Module, ResultsPage, Graded(score: 85, passed: true));

        var recorded = Assert.Single(await db.Events.ToListAsync());
        Assert.Equal(SummativeAssessmentCompleteTracking.EventName, recorded.Name);
        Assert.Equal(42, recorded.UserId);
        AssertRailsAssessmentProperties(recorded.Properties, ResultsPath, 85, success: true);
        Assert.Single(await db.Visits.ToListAsync());
    }

    [Fact]
    public async Task Failed_assessment_writes_success_false()
    {
        await using var db = CreateDb();
        var tracker = CreateTracker(db);

        await tracker.TrackAsync(CreateHttpContext(ResultsPath), 7, Module, ResultsPage, Graded(score: 35, passed: false));

        var recorded = Assert.Single(await db.Events.ToListAsync());
        AssertRailsAssessmentProperties(recorded.Properties, ResultsPath, 35, success: false);
    }

    [Fact]
    public async Task Ungraded_assessment_does_not_write_an_event()
    {
        await using var db = CreateDb();
        var tracker = CreateTracker(db);
        var incomplete = new Assessment { TrainingModule = ModuleName, StartedAt = DateTime.UtcNow };

        await tracker.TrackAsync(CreateHttpContext(ResultsPath), 7, Module, ResultsPage, incomplete);
        await tracker.TrackAsync(CreateHttpContext(ResultsPath), 7, Module, ResultsPage, assessment: null);

        Assert.Empty(await db.Events.ToListAsync());
        Assert.Empty(await db.Visits.ToListAsync());
    }

    [Fact]
    public async Task Repeated_invocation_does_not_write_another_failure_event()
    {
        // Intentional improvement over Rails v1.5.0 ac546721: AssessmentsController
        // re-tracks every failed results show until a pass exists. The shared events
        // table has no uniqueness constraint, so .NET uses the current event API to
        // skip when training_module_id already has the same success value.
        await using var db = CreateDb();
        var tracker = CreateTracker(db);
        var failed = Graded(score: 40, passed: false);
        var context = CreateHttpContext(ResultsPath);

        await tracker.TrackAsync(context, 9, Module, ResultsPage, failed);
        await tracker.TrackAsync(context, 9, Module, ResultsPage, failed);

        Assert.Single(await db.Events.ToListAsync());
    }

    private static SummativeAssessmentCompleteTracker CreateTracker(ApplicationDbContext db) =>
        new(new AuthenticatedKpiEventWriter(db, TimeProvider.System));

    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options,
            new InMemoryNoteBodyProtector());

    private static Assessment Graded(float score, bool passed) =>
        new()
        {
            TrainingModule = ModuleName,
            Score = score,
            Passed = passed,
            CompletedAt = DateTime.UtcNow,
        };

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        return context;
    }

    private static void AssertRailsAssessmentProperties(
        Dictionary<string, object?> properties,
        string path,
        double score,
        bool success)
    {
        Assert.Equal(path, PropertyString(properties, "path"));
        Assert.Equal(SummativeAssessmentCompleteTracking.RailsController, PropertyString(properties, "controller"));
        Assert.Equal(SummativeAssessmentCompleteTracking.RailsAction, PropertyString(properties, "action"));
        Assert.Equal(SummativeAssessmentCompleteTracking.EventType, PropertyString(properties, "type"));
        Assert.Equal(ModuleName, PropertyString(properties, "training_module_id"));
        Assert.Equal("assessment-results", PropertyString(properties, "id"));
        Assert.Equal("results-content-id", PropertyString(properties, "uid"));
        Assert.Equal("module-content-id", PropertyString(properties, "mod_uid"));
        Assert.Equal(score, PropertyNumber(properties, "score"));
        Assert.Equal(success, PropertyBoolean(properties, "success"));
    }

    private static string PropertyString(IReadOnlyDictionary<string, object?> properties, string key)
    {
        Assert.True(properties.ContainsKey(key));
        return properties[key] switch
        {
            string value => value,
            JsonElement element => element.GetString() ?? string.Empty,
            { } value => value.ToString() ?? string.Empty,
            null => string.Empty,
        };
    }

    private static double PropertyNumber(IReadOnlyDictionary<string, object?> properties, string key)
    {
        Assert.True(properties.ContainsKey(key));
        return properties[key] switch
        {
            JsonElement element when element.ValueKind == JsonValueKind.Number => element.GetDouble(),
            IConvertible value => Convert.ToDouble(value),
            _ => throw new InvalidOperationException($"Property '{key}' was not numeric."),
        };
    }

    private static bool PropertyBoolean(IReadOnlyDictionary<string, object?> properties, string key)
    {
        Assert.True(properties.ContainsKey(key));
        return properties[key] switch
        {
            bool value => value,
            JsonElement element when element.ValueKind is JsonValueKind.True or JsonValueKind.False =>
                element.GetBoolean(),
            _ => throw new InvalidOperationException($"Property '{key}' was not boolean."),
        };
    }
}
