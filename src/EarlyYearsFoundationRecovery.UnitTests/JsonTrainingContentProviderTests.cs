using EarlyYearsFoundationRecovery.Infrastructure.Training;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class JsonTrainingContentProviderTests
{
    [Fact]
    public async Task GetModuleByNameAsync_reuses_mapped_module_instances()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var contentRoot = Path.Combine(tempRoot, "app", "bin");
        var dataRoot = Path.Combine(tempRoot, "data");
        Directory.CreateDirectory(contentRoot);
        Directory.CreateDirectory(dataRoot);
        await File.WriteAllTextAsync(
            Path.Combine(dataRoot, "demo-training-content.json"),
            """
            {
              "modules": [
                {
                  "name": "module-one",
                  "title": "Module one",
                  "description": "Demo",
                  "outcomes": "",
                  "criteria": "",
                  "duration": 1,
                  "position": 1,
                  "live": true,
                  "pages": [
                    {
                      "name": "intro",
                      "pageType": "topic_intro",
                      "heading": "Intro",
                      "body": "",
                      "answers": [],
                      "notes": false
                    }
                  ]
                }
              ]
            }
            """);

        try
        {
            var provider = new JsonTrainingContentProvider(
                new StubHostEnvironment(contentRoot),
                NullLogger<JsonTrainingContentProvider>.Instance);

            var first = await provider.GetModuleByNameAsync("module-one");
            var second = await provider.GetModuleByNameAsync("module-one");
            var allModules = await provider.GetAllModulesAsync();

            Assert.NotNull(first);
            Assert.Same(first, second);
            Assert.Same(first, allModules.Single());
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class StubHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "UnitTests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
