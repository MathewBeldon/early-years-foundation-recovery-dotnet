namespace EarlyYearsFoundationRecovery.Web.Services;

public static class ModuleThumbnailUrls
{
    // Placeholder thumbnail used for demo modules that do not supply their own
    // image. Real deployments override this with the module image from the CMS.
    public const string Placeholder = "/images/module-placeholder.png";

    public static string ForModule(string moduleName) => Placeholder;
}
