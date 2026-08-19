using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

internal sealed class NoteEncryptionModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        context is ApplicationDbContext applicationContext
            ? new NoteEncryptionModelCacheKey(
                context.GetType(),
                designTime,
                applicationContext.NoteBodyProtector.ModelCacheKey)
            : new ModelCacheKey(context, designTime);

    private sealed record NoteEncryptionModelCacheKey(
        Type ContextType,
        bool DesignTime,
        string ProtectorIdentity);
}
