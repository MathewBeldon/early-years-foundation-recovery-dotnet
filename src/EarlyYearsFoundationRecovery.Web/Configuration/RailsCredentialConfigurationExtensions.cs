using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Configuration.Memory;

namespace EarlyYearsFoundationRecovery.Web.Configuration;

public static class RailsCredentialConfigurationExtensions
{
    public static ConfigurationManager AddRailsCredentialFallbacks(this ConfigurationManager configuration)
    {
        var fallback = Environment.GetEnvironmentVariable("BOT_TOKEN");
        var values = new Dictionary<string, string?>();
        AddCredential(values, "Audit:BotToken", "AUDIT_BOT_TOKEN", fallback);
        AddCredential(values, "Contentful:WebhookSecret", "CONTENTFUL_WEBHOOK_TOKEN", fallback);
        AddCredential(values, "Notify:CallbackToken", "NOTIFY_WEBHOOK_TOKEN", fallback);

        var source = new MemoryConfigurationSource { InitialData = values };
        var insertionIndex = configuration.Sources
            .Select((candidate, index) => (candidate, index))
            .Where(item => item.candidate is JsonConfigurationSource json &&
                json.Path is not null &&
                Path.GetFileName(json.Path).StartsWith("appsettings", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.index + 1)
            .LastOrDefault();
        configuration.Sources.Insert(insertionIndex, source);
        return configuration;
    }

    private static void AddCredential(
        IDictionary<string, string?> values,
        string configurationKey,
        string environmentVariable,
        string? fallback)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable) ?? fallback;
        if (value is not null)
        {
            values[configurationKey] = value;
        }
    }
}
