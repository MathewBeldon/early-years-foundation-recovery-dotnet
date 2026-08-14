namespace EarlyYearsFoundationRecovery.Infrastructure.Services;

public sealed class NotifyOptions
{
    public const string SectionName = "Notify";
    public string BaseUrl { get; set; } = "http://localhost:4010";
    public string ApiKey { get; set; } = "local-notify-key";
    public string CallbackToken { get; set; } = "local-notify-callback-token";
}
