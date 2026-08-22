namespace EarlyYearsFoundationRecovery.Infrastructure;

public class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    public string StorageRootPath { get; set; } = "storage";

    public string InternalMailbox { get; set; } = "child-development.training@education.gov.uk";

    public string PublicBaseUrl { get; set; } = "http://localhost:5000";

    public static bool IsValid(InfrastructureOptions options) =>
        Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
