using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface INoteRepository
{
    Task<Note?> GetByUserAndPageAsync(
        long userId,
        string trainingModule,
        string pageName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetByUserAndModuleAsync(
        long userId,
        string trainingModule,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Note>> GetByUserAndModulesAsync(
        long userId,
        IEnumerable<string> trainingModules,
        CancellationToken cancellationToken = default);

    Task SaveAsync(Note note, CancellationToken cancellationToken = default);
}
