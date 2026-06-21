using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface ICourseFeedbackRepository
{
    Task<Response?> GetResponseAsync(long userId, string questionName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Response>> GetResponsesAsync(long userId, CancellationToken cancellationToken = default);
    Task<int> CountResponsesAsync(long userId, CancellationToken cancellationToken = default);
    Task SaveResponseAsync(Response response, CancellationToken cancellationToken = default);
}
