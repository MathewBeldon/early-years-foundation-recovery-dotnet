using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Feedback;

public sealed class CourseFeedbackRepository(ApplicationDbContext dbContext) : ICourseFeedbackRepository
{
    public Task<Response?> GetResponseAsync(long userId, string questionName, CancellationToken cancellationToken = default) =>
        dbContext.Responses.FirstOrDefaultAsync(
            r => r.UserId == userId
                && r.TrainingModule == FeedbackFormContent.ModuleName
                && r.QuestionName == questionName,
            cancellationToken);

    public async Task<IReadOnlyList<Response>> GetResponsesAsync(long userId, CancellationToken cancellationToken = default) =>
        await dbContext.Responses
            .Where(r => r.UserId == userId && r.TrainingModule == FeedbackFormContent.ModuleName)
            .ToListAsync(cancellationToken);

    public async Task<int> CountResponsesAsync(long userId, CancellationToken cancellationToken = default) =>
        await dbContext.Responses
            .CountAsync(
                r => r.UserId == userId
                    && r.TrainingModule == FeedbackFormContent.ModuleName
                    && r.QuestionType == "feedback",
                cancellationToken);

    public async Task SaveResponseAsync(Response response, CancellationToken cancellationToken = default)
    {
        if (response.Id == 0)
        {
            dbContext.Responses.Add(response);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
