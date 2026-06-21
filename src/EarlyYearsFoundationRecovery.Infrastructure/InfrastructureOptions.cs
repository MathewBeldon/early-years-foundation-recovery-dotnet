namespace EarlyYearsFoundationRecovery.Infrastructure;

public class InfrastructureOptions
{
    public const string SectionName = "Infrastructure";

    public string StorageRootPath { get; set; } = "storage";

    public string InternalMailbox { get; set; } = "child-development.training@education.gov.uk";
}
