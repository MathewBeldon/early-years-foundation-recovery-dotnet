using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Training;

public sealed class NoteRepository(ApplicationDbContext dbContext) : INoteRepository
{
    public Task<Note?> GetByUserAndPageAsync(
        long userId,
        string trainingModule,
        string pageName,
        CancellationToken cancellationToken = default) =>
        dbContext.Notes
            .FirstOrDefaultAsync(
                n => n.UserId == userId
                    && n.TrainingModule == trainingModule
                    && n.Name == pageName,
                cancellationToken);

    public async Task<IReadOnlyList<Note>> GetByUserAndModuleAsync(
        long userId,
        string trainingModule,
        CancellationToken cancellationToken = default) =>
        await dbContext.Notes
            .AsNoTracking()
            .Where(n => n.UserId == userId && n.TrainingModule == trainingModule)
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Note>> GetByUserAndModulesAsync(
        long userId,
        IEnumerable<string> trainingModules,
        CancellationToken cancellationToken = default)
    {
        var moduleNames = trainingModules
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (moduleNames.Length == 0)
        {
            return [];
        }

        return await dbContext.Notes
            .AsNoTracking()
            .Where(n => n.TrainingModule != null
                && n.UserId == userId
                && moduleNames.Contains(n.TrainingModule))
            .OrderByDescending(n => n.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(Note note, CancellationToken cancellationToken = default)
    {
        if (note.Id == 0)
        {
            dbContext.Notes.Add(note);
        }
        else if (dbContext.Entry(note).State == EntityState.Detached)
        {
            dbContext.Notes.Update(note);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
