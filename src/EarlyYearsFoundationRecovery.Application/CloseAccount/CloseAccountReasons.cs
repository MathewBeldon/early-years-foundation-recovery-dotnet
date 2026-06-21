namespace EarlyYearsFoundationRecovery.Application.CloseAccount;

public static class CloseAccountReasons
{
    public const string NotUseful = "not useful";
    public const string NoTime = "no time";
    public const string NoLongerInEarlyYears = "no longer in early years";
    public const string TooManyEmails = "too many emails";
    public const string Other = "other";
    public const string PreferNotToSay = "prefer not to say";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        NotUseful,
        NoTime,
        NoLongerInEarlyYears,
        TooManyEmails,
        Other,
        PreferNotToSay,
    };
}
