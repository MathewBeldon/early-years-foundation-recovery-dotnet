using EarlyYearsFoundationRecovery.Application.Interfaces;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class TrainingPageContentTests
{
    [Theory]
    [InlineData("sub_module_intro")]
    [InlineData("summary_intro")]
    public void IsSection_returns_true_for_section_page_types(string pageType)
    {
        var page = TrainingPageContent.CreatePage("page", pageType, "Heading", "Body");

        Assert.True(page.IsSection);
    }

    [Theory]
    [InlineData("submodule_intro")]
    [InlineData("sub_module_introduction")]
    [InlineData("summaryintro")]
    [InlineData("summary_intro_extra")]
    public void IsSection_returns_false_for_similarly_named_page_types(string pageType)
    {
        var page = TrainingPageContent.CreatePage("page", pageType, "Heading", "Body");

        Assert.False(page.IsSection);
    }
}
