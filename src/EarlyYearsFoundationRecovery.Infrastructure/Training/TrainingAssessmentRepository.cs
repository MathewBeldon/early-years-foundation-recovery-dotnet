using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EarlyYearsFoundationRecovery.Infrastructure.Training;

public sealed class TrainingAssessmentRepository(ApplicationDbContext dbContext) : ITrainingAssessmentRepository
{
    public Task<Response?> GetResponseAsync(long userId, string moduleName, string questionName, CancellationToken cancellationToken = default) =>
        dbContext.Responses.FirstOrDefaultAsync(
            r => r.UserId == userId &&
                 r.TrainingModule == moduleName &&
                 r.QuestionName == questionName,
            cancellationToken);

    public Task<Response?> GetResponseForAssessmentAsync(
        long userId,
        string moduleName,
        string questionName,
        long assessmentId,
        CancellationToken cancellationToken = default) =>
        dbContext.Responses.FirstOrDefaultAsync(
            r => r.UserId == userId &&
                 r.TrainingModule == moduleName &&
                 r.QuestionName == questionName &&
                 r.AssessmentId == assessmentId,
            cancellationToken);

    public async Task<IReadOnlyList<Response>> GetResponsesForAssessmentAsync(long assessmentId, CancellationToken cancellationToken = default) =>
        await dbContext.Responses
            .AsNoTracking()
            .Where(r => r.AssessmentId == assessmentId)
            .ToListAsync(cancellationToken);

    public Task<Assessment?> GetLatestAssessmentAsync(long userId, string moduleName, bool asNoTracking = false, CancellationToken cancellationToken = default) =>
        (asNoTracking ? dbContext.Assessments.AsNoTracking() : dbContext.Assessments)
            .Where(a => a.UserId == userId && a.TrainingModule == moduleName)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<string, Assessment>> GetLatestAssessmentsByModuleAsync(
        long userId,
        IEnumerable<string> moduleNames,
        CancellationToken cancellationToken = default)
    {
        var names = moduleNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (names.Length == 0)
        {
            return new Dictionary<string, Assessment>(StringComparer.OrdinalIgnoreCase);
        }

        var assessments = await dbContext.Assessments
            .AsNoTracking()
            .Where(a => a.UserId == userId && names.Contains(a.TrainingModule))
            .OrderByDescending(a => a.StartedAt)
            .ThenByDescending(a => a.Id)
            .ToListAsync(cancellationToken);

        return assessments
            .GroupBy(a => a.TrainingModule, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public Task<Assessment?> GetPassedAssessmentAsync(long userId, string moduleName, CancellationToken cancellationToken = default) =>
        dbContext.Assessments
            .Where(a => a.UserId == userId && a.TrainingModule == moduleName && a.Passed == true)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Assessment?> GetIncompleteAssessmentAsync(long userId, string moduleName, CancellationToken cancellationToken = default) =>
        dbContext.Assessments
            .Where(a => a.UserId == userId && a.TrainingModule == moduleName && a.CompletedAt == null)
            .OrderByDescending(a => a.StartedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<Assessment> GetOrCreateIncompleteAssessmentAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken = default)
    {
        var existing = await GetIncompleteAssessmentAsync(userId, moduleName, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var assessment = new Assessment
        {
            UserId = userId,
            TrainingModule = moduleName,
            StartedAt = DateTime.UtcNow,
        };

        dbContext.Assessments.Add(assessment);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return assessment;
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.Entry(assessment).State = EntityState.Detached;
            var createdByConcurrentRequest = await GetIncompleteAssessmentAsync(userId, moduleName, cancellationToken);
            if (createdByConcurrentRequest is null)
            {
                throw;
            }

            return createdByConcurrentRequest;
        }
    }

    public async Task SaveResponseAsync(Response response, CancellationToken cancellationToken = default)
    {
        if (response.Id == 0)
        {
            dbContext.Responses.Add(response);
        }
        else if (dbContext.Entry(response).State == EntityState.Detached)
        {
            dbContext.Responses.Update(response);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveAssessmentAsync(Assessment assessment, CancellationToken cancellationToken = default)
    {
        if (assessment.Id == 0)
        {
            dbContext.Assessments.Add(assessment);
        }
        else if (dbContext.Entry(assessment).State == EntityState.Detached)
        {
            dbContext.Assessments.Update(assessment);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
