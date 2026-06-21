using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface ITrainingAssessmentRepository
{
    Task<Response?> GetResponseAsync(long userId, string moduleName, string questionName, CancellationToken cancellationToken = default);

    Task<Response?> GetResponseForAssessmentAsync(
        long userId,
        string moduleName,
        string questionName,
        long assessmentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Response>> GetResponsesForAssessmentAsync(long assessmentId, CancellationToken cancellationToken = default);

    Task<Assessment?> GetLatestAssessmentAsync(long userId, string moduleName, bool asNoTracking = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, Assessment>> GetLatestAssessmentsByModuleAsync(
        long userId,
        IEnumerable<string> moduleNames,
        CancellationToken cancellationToken = default);

    Task<Assessment?> GetPassedAssessmentAsync(long userId, string moduleName, CancellationToken cancellationToken = default);

    Task<Assessment?> GetIncompleteAssessmentAsync(long userId, string moduleName, CancellationToken cancellationToken = default);

    Task<Assessment> GetOrCreateIncompleteAssessmentAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken = default);

    Task SaveResponseAsync(Response response, CancellationToken cancellationToken = default);

    Task SaveAssessmentAsync(Assessment assessment, CancellationToken cancellationToken = default);
}
