using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EarlyYearsFoundationRecovery.Infrastructure.Notes;

/// <summary>
/// Compatibility implementation of Rails 7.2.3.1 Active Record encryption
/// for Note.body, pinned to Rails commit
/// ac5467218a49c9de58a32a69d4edc01ce37710cf.
///
/// This is a protocol adapter, not a general-purpose application encryption
/// service. It intentionally implements only the non-deterministic body
/// format used by the pinned Rails Note model.
/// </summary>
public sealed class RailsNoteBodyProtector : INoteBodyProtector
{
    public const string RailsVersion = "7.2.3.1";
    public const string RailsCommit = "ac5467218a49c9de58a32a69d4edc01ce37710cf";

    private const int Iterations = 65_536;
    private const int DerivedKeyBytes = 32;
    private const int IvBytes = 12;
    private const int AuthTagBytes = 16;
    private const int CompressionThresholdBytes = 140;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly byte[][] _keys;

    public RailsNoteBodyProtector(
        string primaryKey,
        string keyDerivationSalt,
        IEnumerable<string>? previousPrimaryKeys = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyDerivationSalt);

        var allKeys = new[] { primaryKey }
            .Concat(previousPrimaryKeys ?? [])
            .ToArray();
        if (allKeys.Any(string.IsNullOrWhiteSpace)
            || allKeys.Distinct(StringComparer.Ordinal).Count() != allKeys.Length)
        {
            throw new ArgumentException("Previous note-encryption keys must be non-blank and unique.", nameof(previousPrimaryKeys));
        }

        _keys = allKeys
            .Select(key => Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(key),
                Encoding.UTF8.GetBytes(keyDerivationSalt),
                Iterations,
                HashAlgorithmName.SHA1,
                DerivedKeyBytes))
            .ToArray();

        ModelCacheKey = Guid.NewGuid().ToString("N");
    }

    public string ModelCacheKey { get; }

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var utf8 = Encoding.UTF8.GetBytes(plaintext);
        var compressed = utf8.Length > CompressionThresholdBytes;
        var payload = compressed ? Compress(utf8) : utf8;
        var iv = RandomNumberGenerator.GetBytes(IvBytes);
        var ciphertext = new byte[payload.Length];
        var authTag = new byte[AuthTagBytes];

        using (var aes = new AesGcm(_keys[0], AuthTagBytes))
        {
            aes.Encrypt(iv, payload, ciphertext, authTag, ReadOnlySpan<byte>.Empty);
        }

        return JsonSerializer.Serialize(
            new
            {
                p = Convert.ToBase64String(ciphertext),
                h = new
                {
                    iv = Convert.ToBase64String(iv),
                    at = Convert.ToBase64String(authTag),
                    c = compressed ? true : (bool?)null,
                },
            },
            JsonOptions);
    }

    public string Unprotect(string ciphertext)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);

        foreach (var key in _keys)
        {
            if (TryUnprotect(ciphertext, key, out var plaintext))
            {
                return plaintext;
            }
        }

        // Do not attach the input or cryptographic exception. This exception
        // may be logged by the host and must not become a secret sink.
        throw new NoteBodyEncryptionException(
            "Rails Note.body ciphertext was invalid or could not be decrypted.");
    }

    private static bool TryUnprotect(string ciphertext, byte[] key, out string plaintext)
    {
        plaintext = string.Empty;
        try
        {
            using var document = JsonDocument.Parse(ciphertext);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("p", out var payloadProperty)
                || !root.TryGetProperty("h", out var headers)
                || headers.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var payload = DecodeBase64(payloadProperty);
            var iv = DecodeBase64(headers, "iv");
            var authTag = DecodeBase64(headers, "at");
            var compressed = headers.TryGetProperty("c", out var compressedProperty)
                && compressedProperty.ValueKind == JsonValueKind.True;

            if (iv.Length != IvBytes || authTag.Length != AuthTagBytes)
            {
                return false;
            }

            var decrypted = new byte[payload.Length];
            using (var aes = new AesGcm(key, AuthTagBytes))
            {
                aes.Decrypt(iv, payload, authTag, decrypted, ReadOnlySpan<byte>.Empty);
            }

            plaintext = DecodeUtf8(compressed ? Decompress(decrypted) : decrypted);
            return true;
        }
        catch (Exception exception) when (exception is JsonException
            or InvalidOperationException
            or KeyNotFoundException
            or FormatException
            or CryptographicException
            or InvalidDataException
            or DecoderFallbackException)
        {
            return false;
        }
    }

    private static byte[] DecodeBase64(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text)
        {
            throw new NoteBodyEncryptionException("Rails encryption Base64 value was invalid.");
        }

        return Convert.FromBase64String(text);
    }

    private static byte[] DecodeBase64(JsonElement parent, string propertyName) =>
        parent.TryGetProperty(propertyName, out var value)
            ? DecodeBase64(value)
            : throw new NoteBodyEncryptionException("Rails encryption envelope was incomplete.");

    private static string DecodeUtf8(byte[] bytes) => new UTF8Encoding(false, true).GetString(bytes);

    private static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var deflater = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflater.Write(bytes);
        }

        return output.ToArray();
    }

    private static byte[] Decompress(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var inflater = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        inflater.CopyTo(output);
        return output.ToArray();
    }
}

public sealed class NoteBodyEncryptionException(string message) : Exception(message);

/// <summary>
/// Explicitly registered only by AddInMemoryPersistence for synthetic tests
/// and deterministic demo hosts. It must never be registered for PostgreSQL.
/// </summary>
public sealed class InMemoryNoteBodyProtector : INoteBodyProtector
{
    public string ModelCacheKey { get; } = Guid.NewGuid().ToString("N");

    public string Protect(string plaintext) => plaintext ?? throw new ArgumentNullException(nameof(plaintext));

    public string Unprotect(string ciphertext) => ciphertext ?? throw new ArgumentNullException(nameof(ciphertext));
}
