using System.Text.Json;
using System.Text.Json.Nodes;
using EarlyYearsFoundationRecovery.UnitTests.TestSupport;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class RailsNoteEncryptionInteropTests
{
    private const string RailsVersion = "7.2.3.1";
    private const string RailsCommit = "ac5467218a49c9de58a32a69d4edc01ce37710cf";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void DotNet_decrypts_sanitized_Rails_vectors()
    {
        var keys = LoadKeys();
        var codec = new RailsNoteEncryptionCodec(keys.PrimaryKey, keys.KeyDerivationSalt);
        var document = LoadRailsVectors();

        Assert.Equal(RailsVersion, document.RailsVersion);
        Assert.Equal(RailsCommit, document.RailsCommit);

        foreach (var vector in document.Vectors)
        {
            Assert.Equal(vector.Plaintext, codec.Decrypt(vector.Ciphertext));
            Assert.Equal(vector.Compressed, codec.IsCompressed(vector.Ciphertext));
        }
    }

    [Fact]
    public void DotNet_matches_Rails_compression_threshold_and_envelope_shape()
    {
        var keys = LoadKeys();
        var codec = new RailsNoteEncryptionCodec(keys.PrimaryKey, keys.KeyDerivationSalt);

        var exactlyThreshold = codec.Encrypt(new string('x', 140));
        var overThreshold = codec.Encrypt(new string('x', 141));

        Assert.False(codec.IsCompressed(exactlyThreshold));
        Assert.True(codec.IsCompressed(overThreshold));
        Assert.Equal(new string('x', 140), codec.Decrypt(exactlyThreshold));
        Assert.Equal(new string('x', 141), codec.Decrypt(overThreshold));

        using var json = JsonDocument.Parse(overThreshold);
        Assert.Equal(new[] { "p", "h" }, json.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("p").ValueKind);
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("h").ValueKind);
        Assert.Equal(12, Convert.FromBase64String(json.RootElement.GetProperty("h").GetProperty("iv").GetString()!).Length);
        Assert.Equal(16, Convert.FromBase64String(json.RootElement.GetProperty("h").GetProperty("at").GetString()!).Length);
        Assert.True(json.RootElement.GetProperty("h").GetProperty("c").GetBoolean());

        using var uncompressedJson = JsonDocument.Parse(exactlyThreshold);
        Assert.False(uncompressedJson.RootElement.GetProperty("h").TryGetProperty("c", out _));
    }

    [Fact]
    public void DotNet_encryption_is_nondeterministic()
    {
        var keys = LoadKeys();
        var codec = new RailsNoteEncryptionCodec(keys.PrimaryKey, keys.KeyDerivationSalt);

        var first = codec.Encrypt("A short sanitized note.");
        var second = codec.Encrypt("A short sanitized note.");

        Assert.NotEqual(first, second);
        Assert.Equal("A short sanitized note.", codec.Decrypt(first));
        Assert.Equal("A short sanitized note.", codec.Decrypt(second));
    }

    [Fact]
    public void DotNet_rejects_tampering_and_wrong_key()
    {
        var keys = LoadKeys();
        var codec = new RailsNoteEncryptionCodec(keys.PrimaryKey, keys.KeyDerivationSalt);
        var ciphertext = codec.Encrypt("A short sanitized note.");

        Assert.Throws<RailsNoteEncryptionException>(() => codec.Decrypt(TamperPayload(ciphertext)));
        Assert.Throws<RailsNoteEncryptionException>(() => codec.Decrypt(TamperTag(ciphertext)));

        var wrongCodec = new RailsNoteEncryptionCodec("wrong-test-only-key", keys.KeyDerivationSalt);
        Assert.Throws<RailsNoteEncryptionException>(() => wrongCodec.Decrypt(ciphertext));
    }

    [Fact]
    public void Null_is_rejected_outside_the_string_codec()
    {
        var keys = LoadKeys();
        var codec = new RailsNoteEncryptionCodec(keys.PrimaryKey, keys.KeyDerivationSalt);

        Assert.Throws<ArgumentNullException>(() => codec.Encrypt(null!));
    }

    [Fact]
    public void DotNet_vectors_are_exported_for_the_pinned_Rails_verifier()
    {
        var keys = LoadKeys();
        var codec = new RailsNoteEncryptionCodec(keys.PrimaryKey, keys.KeyDerivationSalt);
        var vectors = LoadRailsVectors().Vectors
            .Select(vector => new
            {
                vector.Name,
                vector.Plaintext,
                Ciphertext = codec.Encrypt(vector.Plaintext),
            })
            .Select(vector => new
            {
                name = vector.Name,
                plaintext = vector.Plaintext,
                ciphertext = vector.Ciphertext,
                compressed = codec.IsCompressed(vector.Ciphertext),
            })
            .ToArray();

        var output = Environment.GetEnvironmentVariable("RAILS_NOTE_INTEROP_DOTNET_OUTPUT")
            ?? Path.Combine(Path.GetTempPath(), $"rails-note-encryption-dotnet-vectors-{Guid.NewGuid():N}.json");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        File.WriteAllText(output, JsonSerializer.Serialize(new
        {
            protocol = "Active Record encryption JSON envelope",
            railsVersion = RailsVersion,
            railsCommit = RailsCommit,
            vectors,
        }, new JsonSerializerOptions { WriteIndented = true }));

        Assert.Equal(LoadRailsVectors().Vectors.Count, vectors.Length);
    }

    private static RailsNoteEncryptionTestKeys LoadKeys()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "tools", "note-encryption-interop", "test-keys.json")));
        var root = document.RootElement;
        return new RailsNoteEncryptionTestKeys(
            root.GetProperty("primaryKey").GetString()!,
            root.GetProperty("deterministicKey").GetString()!,
            root.GetProperty("keyDerivationSalt").GetString()!);
    }

    private static RailsVectorDocument LoadRailsVectors()
    {
        var path = Environment.GetEnvironmentVariable("RAILS_NOTE_INTEROP_VECTORS")
            ?? Path.Combine(RepositoryRoot(), "tools", "note-encryption-interop", "fixtures", "rails-vectors.json");
        return JsonSerializer.Deserialize<RailsVectorDocument>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidOperationException("Rails vector fixture was empty.");
    }

    private static string TamperPayload(string ciphertext)
    {
        var json = JsonNode.Parse(ciphertext)!.AsObject();
        var payload = Convert.FromBase64String(json["p"]!.GetValue<string>());
        payload[0] ^= 1;
        json["p"] = Convert.ToBase64String(payload);
        return json.ToJsonString();
    }

    private static string TamperTag(string ciphertext)
    {
        var json = JsonNode.Parse(ciphertext)!.AsObject();
        var tag = Convert.FromBase64String(json["h"]!["at"]!.GetValue<string>());
        tag[0] ^= 1;
        json["h"]!["at"] = Convert.ToBase64String(tag);
        return json.ToJsonString();
    }

    private static string RepositoryRoot() => RailsContractConsistencyTests.RepositoryRoot();

    private sealed record RailsNoteEncryptionTestKeys(string PrimaryKey, string DeterministicKey, string KeyDerivationSalt);

    private sealed record RailsVectorDocument(string RailsVersion, string RailsCommit, List<RailsVector> Vectors);

    private sealed record RailsVector(string Name, string Plaintext, string Ciphertext, bool Compressed);
}
