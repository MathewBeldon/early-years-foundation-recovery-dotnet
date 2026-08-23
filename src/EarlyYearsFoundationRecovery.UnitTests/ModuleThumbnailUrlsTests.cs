using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Web.Services;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ModuleThumbnailUrlsTests
{
    [Fact]
    public void Uses_mapped_thumbnail()
    {
        var module = Module("https://images.ctfassets.net/space/image/module.jpg");

        Assert.Equal(module.ThumbnailUrl, ModuleThumbnailUrls.ForModule(module));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Falls_back_when_thumbnail_is_unavailable(string? thumbnailUrl)
    {
        Assert.Equal(ModuleThumbnailUrls.Placeholder, ModuleThumbnailUrls.ForModule(Module(thumbnailUrl)));
    }

    private static TrainingModuleContent Module(string? thumbnailUrl) => new(
        "module-1", "Module one", "Description", "Outcomes", "Criteria", 120, 1, true, [],
        ThumbnailUrl: thumbnailUrl);
}
