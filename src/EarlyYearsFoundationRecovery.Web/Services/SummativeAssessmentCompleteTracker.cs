using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Web.Services;

public sealed class SummativeAssessmentCompleteTracker(AuthenticatedKpiEventWriter kpiEvents)
{
    public async Task TrackAsync(
        HttpContext httpContext,
        long userId,
        string moduleName,
        Assessment? assessment,
        CancellationToken cancellationToken = default)
    {
        var existing = await kpiEvents.ListNamedEventsAsync(
            userId,
            SummativeAssessmentCompleteTracking.EventName,
            cancellationToken);
        if (assessment is null
            || !SummativeAssessmentCompleteTracking.ShouldRecord(assessment, moduleName, existing))
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
            SummativeAssessmentCompleteTracking.CreateProperties(moduleName, assessment));
    }
}
