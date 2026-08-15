using System.Text.Json;
using System.Text.RegularExpressions;
using System.Diagnostics;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;

namespace EarlyYearsFoundationRecovery.UnitTests;

/// <summary>
/// Integrity of the pinned Rails contract itself, as distinct from semantic
/// conformance to Rails behaviour.
///
/// The workspace once validated against a Rails commit three months stale while
/// asserting it was authoritative: everything agreed with the pin, and nobody
/// checked the pin. These tests make internal desynchronisation a red test.
/// They cannot detect that the pin has fallen behind upstream — that needs the
/// network, and lives in './parity.ps1 check-pin'.
/// </summary>
public sealed partial class RailsContractConsistencyTests
{
    // Kept identical to $refreshWorktreeCommand in parity.ps1 so both routes to
    // this failure hand back the same remediation.
    private const string RefreshWorktreeCommand = "git worktree remove --force parity/.rails-source";

    [Fact]
    public void Contract_manifest_fields_are_well_formed()
    {
        var contract = LoadContract();

        Assert.Matches("^[0-9a-f]{40}$", contract.Commit);
        Assert.Matches(@"^\d{14}$", contract.SchemaVersion);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", contract.ReviewedOn);
        Assert.True(DateOnly.TryParse(contract.ReviewedOn, out _), $"reviewedOn '{contract.ReviewedOn}' is not a real date.");
        Assert.False(string.IsNullOrWhiteSpace(contract.ReleaseRef));
        Assert.False(string.IsNullOrWhiteSpace(contract.Upstream));
    }

    [Fact]
    public void Required_rails_version_matches_the_contract_manifest()
    {
        var contract = LoadContract();

        // The manifest is the source of truth. RequiredRailsVersion is the one
        // permitted duplicate, because production code must not read parity/.
        Assert.True(
            string.Equals(RailsSchemaCompatibility.RequiredRailsVersion, contract.SchemaVersion, StringComparison.Ordinal),
            $"RequiredRailsVersion is {RailsSchemaCompatibility.RequiredRailsVersion} but rails-contract.json records {contract.SchemaVersion}. Advance both together.");
    }

    [Fact]
    public void Parity_script_takes_the_pin_from_the_manifest_rather_than_hard_coding_it()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "parity.ps1"));

        Assert.Contains("rails-contract.json", script, StringComparison.Ordinal);
        Assert.Contains("$contract.commit", script, StringComparison.Ordinal);
        Assert.DoesNotMatch(ShaLiteral(), script);
        Assert.DoesNotMatch(VersionTagLiteral(), script);
    }

    [RailsContractFact(RailsContractLocalSetup.UpstreamRemote)]
    public void Manifest_upstream_matches_the_configured_git_remote()
    {
        var contract = LoadContract();
        var configured = ConfiguredUpstreamRemote();

        Assert.True(
            string.Equals(NormalizeRemote(configured!), NormalizeRemote(contract.Upstream), StringComparison.OrdinalIgnoreCase),
            $"rails-contract.json records upstream '{contract.Upstream}' but git remote 'upstream' is configured as '{configured}'.");
    }

    [Fact]
    public void Parity_script_and_these_tests_offer_the_same_worktree_remediation()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot(), "parity.ps1"));

        Assert.Contains(RefreshWorktreeCommand, script, StringComparison.Ordinal);
    }

    [RailsContractFact(RailsContractLocalSetup.PinnedWorktree)]
    public void Pinned_worktree_matches_the_contract_when_it_is_present()
    {
        var contract = LoadContract();
        var worktree = Path.Combine(RepositoryRoot(), "parity", ".rails-source");

        var head = ReadDetachedHead(worktree);
        Assert.True(
            string.Equals(head, contract.Commit, StringComparison.Ordinal),
            $"parity/.rails-source is at {head} but the contract pins {contract.ReleaseRef} ({contract.Commit}). Run '{RefreshWorktreeCommand}' and rerun.");

        var schema = File.ReadLines(Path.Combine(worktree, "db", "schema.rb")).Take(30);
        var declared = schema
            .Select(line => SchemaVersion().Match(line))
            .FirstOrDefault(match => match.Success)?.Groups[1].Value.Replace("_", string.Empty);
        Assert.True(
            string.Equals(declared, contract.SchemaVersion, StringComparison.Ordinal),
            $"parity/.rails-source declares schema {declared ?? "<none>"} but the contract records {contract.SchemaVersion}. Run '{RefreshWorktreeCommand}' and rerun.");
    }

    private static RailsContract LoadContract()
    {
        var path = Path.Combine(RepositoryRoot(), "parity", "rails-contract.json");
        Assert.True(File.Exists(path), $"Missing contract manifest at {path}.");
        return JsonSerializer.Deserialize<RailsContract>(
            File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    /// <summary>
    /// Reads the worktree HEAD without shelling out. A linked worktree's .git is
    /// a file naming its real gitdir, whose HEAD holds the SHA when detached.
    /// </summary>
    private static string ReadDetachedHead(string worktree)
    {
        var dotGit = Path.Combine(worktree, ".git");
        var gitDir = File.Exists(dotGit)
            ? File.ReadAllText(dotGit).Trim()["gitdir:".Length..].Trim()
            : dotGit;
        var head = File.ReadAllText(Path.Combine(gitDir, "HEAD")).Trim();
        Assert.DoesNotContain("ref:", head, StringComparison.Ordinal);
        return head;
    }

    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.EnumerateFiles("*.slnx").Any())
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root from the test assembly.");
    }

    internal static string? ConfiguredUpstreamRemote()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = RepositoryRoot(),
            },
        };
        process.StartInfo.ArgumentList.Add("config");
        process.StartInfo.ArgumentList.Add("--get");
        process.StartInfo.ArgumentList.Add("remote.upstream.url");
        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();
        return process.ExitCode == 0 && output.Length > 0 ? output : null;
    }

    private static string NormalizeRemote(string remote) =>
        remote.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? remote[..^4] : remote;

    // A literal SHA or release tag in parity.ps1 would be a second source of
    // truth. Regex *patterns* mentioning them are not literals and do not match.
    [GeneratedRegex(@"\b[0-9a-f]{7,40}\b")] private static partial Regex ShaLiteral();
    [GeneratedRegex(@"""v[0-9]+\.[0-9]+\.[0-9]+""")] private static partial Regex VersionTagLiteral();
    [GeneratedRegex(@"define\(version:\s*([0-9_]+)\s*\)")] private static partial Regex SchemaVersion();

    private sealed record RailsContract(string Upstream, string ReleaseRef, string Commit, string SchemaVersion, string ReviewedOn);
}

public enum RailsContractLocalSetup
{
    PinnedWorktree,
    UpstreamRemote,
}

public sealed class RailsContractFactAttribute : FactAttribute
{
    public RailsContractFactAttribute(RailsContractLocalSetup setup)
    {
        if (setup == RailsContractLocalSetup.PinnedWorktree &&
            !Directory.Exists(Path.Combine(RailsContractConsistencyTests.RepositoryRoot(), "parity", ".rails-source")))
        {
            Skip = "parity/.rails-source is absent; the pinned Rails worktree was not checked.";
        }
        else if (setup == RailsContractLocalSetup.UpstreamRemote &&
                 RailsContractConsistencyTests.ConfiguredUpstreamRemote() is null)
        {
            Skip = "Git remote 'upstream' is not configured; the manifest upstream was not checked.";
        }
    }
}
