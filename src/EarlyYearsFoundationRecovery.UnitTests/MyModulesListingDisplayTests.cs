using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class MyModulesListingDisplayTests
{
    [Fact]
    public void ModuleTitlePath_matches_rails_training_module_path()
    {
        // Rails v1.5.0 ac546721 app/views/learning/_card.html.slim uses training_module_path(mod.name).
        Assert.Equal("/modules/module-1", MyModulesListingDisplay.ModuleTitlePath("module-1"));
    }

    [Fact]
    public void ShowAvailableDescription_is_false()
    {
        // Rails v1.5.0 ac546721 app/views/learning/_available_modules.html.slim passes show_module_description: false.
        Assert.False(MyModulesListingDisplay.ShowAvailableDescription);
    }

    [Fact]
    public void ShowUpcomingAboutLink_follows_rails_draft_guard()
    {
        // Rails v1.5.0 ac546721 app/views/learning/_upcoming_modules.html.slim: unless mod.draft?
        Assert.False(MyModulesListingDisplay.ShowUpcomingAboutLink(isDraft: true));
        Assert.True(MyModulesListingDisplay.ShowUpcomingAboutLink(isDraft: false));
    }

    [Fact]
    public void UnstartedCopy_matches_rails_begin_training_course()
    {
        // Rails v1.5.0 ac546721 config/locales/en.yml my_learning.begin_training_course.
        Assert.Equal(
            "You have not started any modules. To begin the training course, start an available module.",
            MyModulesListingDisplay.UnstartedCopy);
    }
}
