namespace EarlyYearsFoundationRecovery.ParityTests;

/// <summary>
/// Resolves the live parity environment.
///
/// These tests compare two running applications, so an unconfigured environment
/// is a setup error rather than a pass. Reporting it as success made the suite
/// claim migration evidence it had not gathered.
/// </summary>
public static class ParityEnvironment
{
    public const string RailsUrlVariable = "RAILS_BASE_URL";
    public const string DotnetUrlVariable = "DOTNET_BASE_URL";
    public const string OptOutVariable = "PARITY_ENV_OPTIONAL";

    public static string? RailsBaseUrl => Value(RailsUrlVariable);

    public static string? DotnetBaseUrl => Value(DotnetUrlVariable);

    public static bool IsConfigured => RailsBaseUrl is not null && DotnetBaseUrl is not null;

    /// <summary>
    /// A configured environment always runs, so an opt-out left in the shell can
    /// never quietly disable a parity environment that is actually present.
    /// </summary>
    public static bool IsSkipped => !IsConfigured && Value(OptOutVariable) is not null;

    public static string SkipReason =>
        $"{OptOutVariable} is set and no parity environment is configured. " +
        "This suite proves nothing in this run.";

    public static string SetupMessage =>
        $"""
        The parity suite compares two running applications and cannot pass without them.

        To run it:            ./parity.ps1 reset; ./parity.ps1 test
        Or set manually:      {RailsUrlVariable} and {DotnetUrlVariable}
        To exclude it from a solution-wide run:
                              dotnet test --filter "Category!=Parity"
                              (or set {OptOutVariable}=1 to report these as skipped)

        Currently {RailsUrlVariable}={Describe(RailsBaseUrl)}, {DotnetUrlVariable}={Describe(DotnetBaseUrl)}.
        """;

    /// <summary>Both base URLs, failing with setup guidance when either is absent.</summary>
    public static (string Rails, string Dotnet) Require()
    {
        Assert.True(IsConfigured, SetupMessage);
        return (RailsBaseUrl!, DotnetBaseUrl!);
    }

    /// <summary>The .NET base URL only, for checks that do not call Rails.</summary>
    public static string RequireDotnet()
    {
        Assert.True(DotnetBaseUrl is not null, SetupMessage);
        return DotnetBaseUrl!;
    }

    private static string? Value(string name) =>
        Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    private static string Describe(string? value) => value ?? "<unset>";
}

/// <summary>
/// A parity test. Reports as skipped only when the environment is absent *and*
/// the opt-out is set; otherwise it runs and fails loudly on missing setup.
/// </summary>
public sealed class ParityFactAttribute : FactAttribute
{
    public ParityFactAttribute()
    {
        if (ParityEnvironment.IsSkipped)
        {
            Skip = ParityEnvironment.SkipReason;
        }
    }
}
