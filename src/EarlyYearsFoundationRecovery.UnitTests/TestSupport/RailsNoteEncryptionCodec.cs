using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EarlyYearsFoundationRecovery.UnitTests.TestSupport;

internal sealed class RailsNoteEncryptionException : Exception
{
    public RailsNoteEncryptionException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Test-only reproduction of the Rails 7.2.3.1 Active Record encryption
/// protocol used by Note.body. This type must not be moved into production
/// code without a separately approved compatibility decision.
/// </summary>
internal sealed class RailsNoteEncryptionCodec
{
    private const int Iterations = 65_536;
    private const int DerivedKeyBytes = 32;
    private const int IvBytes = 12;
    private const int AuthTagBytes = 16;
    private const int CompressionThresholdBytes = 140;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly byte[] _key;

    public RailsNoteEncryptionCodec(string primaryKey, string keyDerivationSalt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyDerivationSalt);

        _key = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(primaryKey),
            Encoding.UTF8.GetBytes(keyDerivationSalt),
            Iterations,
            HashAlgorithmName.SHA1,
            DerivedKeyBytes);
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var utf8 = Encoding.UTF8.GetBytes(plaintext);
        var compressed = utf8.Length > CompressionThresholdBytes;
        var payload = compressed ? Compress(utf8) : utf8;
        var iv = RandomNumberGenerator.GetBytes(IvBytes);
        var ciphertext = new byte[payload.Length];
        var authTag = new byte[AuthTagBytes];

        using (var aes = new AesGcm(_key, AuthTagBytes))
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

    public string Decrypt(string encryptedText)
    {
        ArgumentNullException.ThrowIfNull(encryptedText);

        try
        {
            using var document = JsonDocument.Parse(encryptedText);
            var root = document.RootElement;
            var payload = DecodeBase64(root, "p");
            var headers = root.GetProperty("h");
            var iv = DecodeBase64(headers, "iv");
            var authTag = DecodeBase64(headers, "at");
            var compressed = headers.TryGetProperty("c", out var compressedProperty)
                && compressedProperty.ValueKind == JsonValueKind.True;

            if (iv.Length != IvBytes || authTag.Length != AuthTagBytes)
            {
                throw new RailsNoteEncryptionException("Rails encryption header lengths are invalid.");
            }

            var plaintext = new byte[payload.Length];
            using (var aes = new AesGcm(_key, AuthTagBytes))
            {
                aes.Decrypt(iv, payload, authTag, plaintext, ReadOnlySpan<byte>.Empty);
            }

            return DecodeUtf8(compressed ? Decompress(plaintext) : plaintext);
        }
        catch (RailsNoteEncryptionException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException
            or KeyNotFoundException
            or FormatException
            or CryptographicException
            or InvalidDataException
            or DecoderFallbackException)
        {
            throw new RailsNoteEncryptionException("Rails Note.body ciphertext was invalid or could not be decrypted.", exception);
        }
    }

    public bool IsCompressed(string encryptedText)
    {
        using var document = JsonDocument.Parse(encryptedText);
        return document.RootElement.GetProperty("h").TryGetProperty("c", out var property)
            && property.ValueKind == JsonValueKind.True;
    }

    private static byte[] DecodeBase64(JsonElement parent, string propertyName)
    {
        var value = parent.GetProperty(propertyName).GetString()
            ?? throw new RailsNoteEncryptionException("Rails encryption Base64 value was null.");
        return Convert.FromBase64String(value);
    }

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
