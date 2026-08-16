using EarlyYearsFoundationRecovery.Application.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class LearningLogRedirectTests
{
    // Rails v1.5.0 ac546721 Training::NotesController#next_page_path does not
    // follow an arbitrary next_page_url; external values must fall through.
    [Theory]
    [InlineData("https://evil.example/phish")]
    [InlineData("http://evil.example/phish")]
    [InlineData("//evil.example/phish")]
    [InlineData("/\\evil.example")]
    [InlineData("https://localhost/modules/module-1/content-pages/intro")]
    [InlineData("javascript:alert(1)")]
    [InlineData("modules/module-1/content-pages/intro")]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveNextPagePath_rejects_external_or_non_application_urls(string nextPageUrl)
    {
        var destination = LearningLogRedirect.ResolveNextPagePath(
            nextPageUrl,
            nextPageModule: null,
            nextPageName: null,
            trainingModule: null,
            pageName: null);

        Assert.Equal(LearningLogRedirect.LearningLogPath, destination);
        Assert.False(LearningLogRedirect.IsLocalApplicationPath(nextPageUrl));
    }

    [Theory]
    [InlineData("/modules/module-1/content-pages/intro")]
    [InlineData("/modules/module-1/questionnaires/check-understanding")]
    [InlineData("/my-account/learning-log")]
    [InlineData("/my-modules")]
    [InlineData("/modules/module-1/content-pages/intro?from=note")]
    public void ResolveNextPagePath_honours_local_application_paths(string nextPageUrl)
    {
        var destination = LearningLogRedirect.ResolveNextPagePath(
            nextPageUrl,
            nextPageModule: "ignored-module",
            nextPageName: "ignored-page",
            trainingModule: "also-ignored",
            pageName: "also-ignored");

        Assert.Equal(nextPageUrl, destination);
        Assert.True(LearningLogRedirect.IsLocalApplicationPath(nextPageUrl));
    }

    [Fact]
    public void ResolveNextPagePath_falls_back_to_next_page_module_and_name()
    {
        // Rails v1.5.0 ac546721 Training::NotesController#next_page_path:63-64.
        var destination = LearningLogRedirect.ResolveNextPagePath(
            "https://evil.example/phish",
            nextPageModule: "module-1",
            nextPageName: "applying-learning",
            trainingModule: "module-1",
            pageName: "intro");

        Assert.Equal("/modules/module-1/content-pages/applying-learning", destination);
    }

    [Fact]
    public void ResolveNextPagePath_falls_back_to_current_page_then_learning_log()
    {
        // Rails v1.5.0 ac546721 Training::NotesController#next_page_path:65-69.
        Assert.Equal(
            "/modules/module-1/content-pages/intro",
            LearningLogRedirect.ResolveNextPagePath(
                null,
                nextPageModule: null,
                nextPageName: null,
                trainingModule: "module-1",
                pageName: "intro"));

        Assert.Equal(
            LearningLogRedirect.LearningLogPath,
            LearningLogRedirect.ResolveNextPagePath(null, null, null, null, null));
    }
}
