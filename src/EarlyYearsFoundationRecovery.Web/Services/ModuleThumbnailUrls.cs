using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.Web.Services;

public static class ModuleThumbnailUrls
{
    // Placeholder used when the content provider cannot supply a trusted image.
    public const string Placeholder = "/images/module-placeholder.png";

    public static string ForModule(TrainingModuleContent module) =>
        string.IsNullOrWhiteSpace(module.ThumbnailUrl) ? Placeholder : module.ThumbnailUrl;
}
