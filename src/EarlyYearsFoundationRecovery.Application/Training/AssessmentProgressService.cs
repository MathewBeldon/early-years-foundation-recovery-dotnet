using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed class AssessmentProgressService(ITrainingAssessmentRepository assessmentRepository)
{
    public Task<Assessment?> GetLatestAssessmentAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken = default) =>
        assessmentRepository.GetLatestAssessmentAsync(userId, moduleName, cancellationToken: cancellationToken);

    public static bool IsGraded(Assessment? assessment) =>
        assessment?.Score is not null;

    public static bool IsPassed(Assessment? assessment) =>
        assessment?.Passed == true;

    public static bool IsFailed(Assessment? assessment) =>
        IsGraded(assessment) && assessment?.Passed != true;

    public async Task<Assessment> ResolveSummativeAssessmentAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        var passed = await assessmentRepository.GetPassedAssessmentAsync(userId, moduleName, cancellationToken);
        if (passed is not null)
        {
            return passed;
        }

        return await assessmentRepository.GetOrCreateIncompleteAssessmentAsync(userId, moduleName, cancellationToken);
    }
}
