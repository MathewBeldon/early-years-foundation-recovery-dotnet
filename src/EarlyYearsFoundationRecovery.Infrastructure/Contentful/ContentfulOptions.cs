namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulOptions
{
    public const string SectionName = "Contentful";

    public string SpaceId { get; set; } = string.Empty;

    public string Environment { get; set; } = "master";

    public string DeliveryApiKey { get; set; } = string.Empty;

    public int CacheMinutes { get; set; } = 5;

    public string? WebhookSecret { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SpaceId) &&
        !string.IsNullOrWhiteSpace(DeliveryApiKey);
}
