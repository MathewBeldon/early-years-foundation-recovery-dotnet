using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EarlyYearsFoundationRecovery.IntegrationTests;

public sealed class ModuleThumbnailRenderingTests
{
    [Fact]
    public async Task Public_module_card_renders_trusted_thumbnail_as_decorative()
    {
        await using var factory = new ThumbnailWebApplicationFactory(
            "https://images.ctfassets.net/space/image/module.jpg");
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains(
            "<img src=\"https://images.ctfassets.net/space/image/module.jpg\" alt=\"\" width=\"800\" height=\"450\" />",
            html,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Public_module_card_renders_placeholder_when_thumbnail_is_missing()
    {
        await using var factory = new ThumbnailWebApplicationFactory(null);
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains(
            "<img src=\"/images/module-placeholder.png\" alt=\"\" width=\"800\" height=\"450\" />",
            html,
            StringComparison.Ordinal);
    }

    private sealed class ThumbnailWebApplicationFactory(string? thumbnailUrl) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            IntegrationTestHost.Configure(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITrainingContentProvider>();
                services.AddSingleton<ITrainingContentProvider>(new ThumbnailContentProvider(thumbnailUrl));
            });
        }
    }

    private sealed class ThumbnailContentProvider : ITrainingContentProvider
    {
        private readonly TrainingModuleContent _module;

        public ThumbnailContentProvider(string? thumbnailUrl)
        {
            _module = new TrainingModuleContent(
                "module-1",
                "Module one",
                "Description",
                "Outcomes",
                "Criteria",
                120,
                1,
                true,
                [],
                ThumbnailUrl: thumbnailUrl);
        }

        public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrainingModuleContent>>([_module]);

        public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default) =>
            GetLiveModulesAsync(cancellationToken);

        public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default) =>
            Task.FromResult<TrainingModuleContent?>(_module);

        public Task<TrainingPageContent?> GetPageAsync(string moduleName, string pageName, CancellationToken cancellationToken = default) =>
            Task.FromResult<TrainingPageContent?>(null);
    }
}
