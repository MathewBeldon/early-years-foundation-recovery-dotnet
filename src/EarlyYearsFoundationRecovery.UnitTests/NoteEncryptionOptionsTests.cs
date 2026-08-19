using EarlyYearsFoundationRecovery.Infrastructure.Notes;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class NoteEncryptionOptionsTests
{
    [Fact]
    public void Missing_primary_key_is_invalid()
    {
        var result = NoteEncryptionOptions.Validate(new NoteEncryptionOptions
        {
            KeyDerivationSalt = "salt",
        });

        Assert.False(result.IsValid);
        Assert.Contains("PrimaryKey", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_salt_is_invalid()
    {
        var result = NoteEncryptionOptions.Validate(new NoteEncryptionOptions
        {
            PrimaryKey = "key",
        });

        Assert.False(result.IsValid);
        Assert.Contains("KeyDerivationSalt", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Blank_or_duplicate_previous_keys_are_invalid()
    {
        Assert.False(NoteEncryptionOptions.Validate(new NoteEncryptionOptions
        {
            PrimaryKey = "key",
            KeyDerivationSalt = "salt",
            PreviousPrimaryKeys = [" ", "old"],
        }).IsValid);

        Assert.False(NoteEncryptionOptions.Validate(new NoteEncryptionOptions
        {
            PrimaryKey = "key",
            KeyDerivationSalt = "salt",
            PreviousPrimaryKeys = ["old", "old"],
        }).IsValid);
    }

    [Fact]
    public void Current_key_precedes_previous_keys_in_the_rotation_protocol()
    {
        var result = NoteEncryptionOptions.Validate(new NoteEncryptionOptions
        {
            PrimaryKey = "current",
            KeyDerivationSalt = "salt",
            PreviousPrimaryKeys = ["old-1", "old-2"],
        });

        Assert.True(result.IsValid, result.Message);
        var protector = new RailsNoteBodyProtector("current", "salt", ["old-1", "old-2"]);
        var oldCiphertext = new RailsNoteBodyProtector("old-2", "salt").Protect("old note");
        Assert.Equal("old note", protector.Unprotect(oldCiphertext));
    }
}
