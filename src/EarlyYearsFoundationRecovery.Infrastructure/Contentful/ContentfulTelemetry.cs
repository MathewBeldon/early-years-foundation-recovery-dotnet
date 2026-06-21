using System.Diagnostics;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public static class ContentfulTelemetry
{
    public const string ActivitySourceName = "EarlyYearsFoundationRecovery.Contentful";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
