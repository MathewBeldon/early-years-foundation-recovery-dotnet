namespace EarlyYearsFoundationRecovery.Infrastructure.Notes;

/// <summary>
/// Protects the plaintext value used by the application from the ciphertext
/// persisted in the Rails-owned notes.body column.
/// </summary>
public interface INoteBodyProtector
{
    /// <summary>Protects a non-null plaintext note body for persistence.</summary>
    string Protect(string plaintext);

    /// <summary>Decrypts a persisted note body into application plaintext.</summary>
    string Unprotect(string ciphertext);

    /// <summary>
    /// An in-process identity used only to keep EF models from sharing a value
    /// converter that captured a different protector. It is never logged or
    /// persisted.
    /// </summary>
    string ModelCacheKey { get; }
}
