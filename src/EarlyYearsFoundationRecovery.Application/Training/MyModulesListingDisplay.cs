namespace EarlyYearsFoundationRecovery.Application.Training;

/// <summary>
/// Stable My modules listing semantics from Rails v1.5.0 commit ac546721.
/// Does not classify live/draft buckets; that remains a known blocker.
/// </summary>
public static class MyModulesListingDisplay
{
    public const string UnstartedCopy =
        "You have not started any modules. To begin the training course, start an available module.";

    /// <summary>
    /// Rails v1.5.0 <c>app/views/learning/_card.html.slim</c> sets
    /// <c>destination = training_module_path(mod.name)</c> for signed-in cards.
    /// </summary>
    public static string ModuleTitlePath(string moduleName) => $"/modules/{moduleName}";

    /// <summary>
    /// Rails v1.5.0 <c>app/views/learning/_available_modules.html.slim</c> renders
    /// available cards with <c>show_module_description: false</c>.
    /// </summary>
    public static bool ShowAvailableDescription => false;

    /// <summary>
    /// Rails v1.5.0 <c>app/views/learning/_upcoming_modules.html.slim</c> emits the
    /// "View more information" link only <c>unless mod.draft?</c>.
    /// </summary>
    public static bool ShowUpcomingAboutLink(bool isDraft) => !isDraft;
}
