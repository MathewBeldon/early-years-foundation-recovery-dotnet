using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Models;

namespace EarlyYearsFoundationRecovery.Web.Services;

public static class PublicModuleViewModelBuilder
{
    public static async Task<IReadOnlyList<PublicModuleCardViewModel>> BuildAsync(
        ITrainingContentProvider contentProvider,
        bool isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        var modules = await contentProvider.GetLiveModulesAsync(cancellationToken);
        return MapModules(modules, isAuthenticated);
    }

    public static async Task<IReadOnlyList<PublicModuleCardViewModel>> BuildAllAsync(
        ITrainingContentProvider contentProvider,
        bool isAuthenticated,
        CancellationToken cancellationToken = default)
    {
        var modules = await contentProvider.GetAllModulesAsync(cancellationToken);
        return MapModules(modules, isAuthenticated);
    }

    public static IReadOnlyList<PublicModuleCardViewModel> MapModules(
        IReadOnlyList<TrainingModuleContent> modules,
        bool isAuthenticated) =>
        modules.Select(module => new PublicModuleCardViewModel
        {
            Name = module.Name,
            Title = module.Title,
            Description = module.Description,
            Position = module.Position,
            Live = module.Live,
            ModuleUrl = isAuthenticated && module.Live
                ? $"/modules/{module.Name}"
                : $"/about/{module.Name}",
            ThumbnailUrl = ModuleThumbnailUrls.ForModule(module.Name),
        }).ToList();
}
