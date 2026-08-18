using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Web.Services;

public sealed class SummativeAssessmentCompleteTracker(AuthenticatedKpiEventWriter kpiEvents)
{
    public async Task TrackAsync(
        HttpContext httpContext,
        long userId,
        TrainingModuleContent module,
        TrainingPageContent resultsPage,
        Assessment? assessment,
        CancellationToken cancellationToken = default)
    {
        var existing = await kpiEvents.ListNamedEventsAsync(
            userId,
            SummativeAssessmentCompleteTracking.EventName,
            cancellationToken);
        if (assessment is null
            || !SummativeAssessmentCompleteTracking.ShouldRecord(assessment, module.Name, existing))
        {
            return;
        }

        await kpiEvents.TrackAsync(
            httpContext,
            userId,
            SummativeAssessmentCompleteTracking.EventName,
            SummativeAssessmentCompleteTracking.RailsController,
            SummativeAssessmentCompleteTracking.RailsAction,
            cancellationToken,
            SummativeAssessmentCompleteTracking.CreateProperties(module, resultsPage, assessment));
    }
}
