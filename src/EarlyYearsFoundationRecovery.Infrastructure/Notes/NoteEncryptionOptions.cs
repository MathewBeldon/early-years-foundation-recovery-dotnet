namespace EarlyYearsFoundationRecovery.Infrastructure.Notes;

/// <summary>
/// Rails Active Record encryption material for the Rails 7.2.3.1
/// (ac5467218a49c9de58a32a69d4edc01ce37710cf) Note.body protocol.
///
/// The values are deployment secrets. There are deliberately no defaults.
/// </summary>
public sealed class NoteEncryptionOptions
{
    public const string SectionName = "NoteEncryption";

    public string? PrimaryKey { get; set; }

    public string? KeyDerivationSalt { get; set; }

    public List<string> PreviousPrimaryKeys { get; set; } = [];

    public static NoteEncryptionValidationResult Validate(NoteEncryptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.PrimaryKey))
        {
            return new(false, "NoteEncryption:PrimaryKey is missing or blank.");
        }

        if (string.IsNullOrWhiteSpace(options.KeyDerivationSalt))
        {
            return new(false, "NoteEncryption:KeyDerivationSalt is missing or blank.");
        }

        if (options.PreviousPrimaryKeys is null)
        {
            return new(false, "NoteEncryption:PreviousPrimaryKeys must be an ordered list when supplied.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            options.PrimaryKey,
        };

        for (var index = 0; index < options.PreviousPrimaryKeys.Count; index++)
        {
            var previousKey = options.PreviousPrimaryKeys[index];
            if (string.IsNullOrWhiteSpace(previousKey))
            {
                return new(false, $"NoteEncryption:PreviousPrimaryKeys[{index}] is blank.");
            }

            if (!keys.Add(previousKey))
            {
                return new(false, "NoteEncryption:PreviousPrimaryKeys contains a duplicate key.");
            }
        }

        return new(true, "Note encryption configuration is present and valid.");
    }
}

public sealed record NoteEncryptionValidationResult(bool IsValid, string Message);
