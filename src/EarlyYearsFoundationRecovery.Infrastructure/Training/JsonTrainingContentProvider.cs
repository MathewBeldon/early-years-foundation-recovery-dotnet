using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Training;

public sealed class JsonTrainingContentProvider : ITrainingContentProvider
{
    private readonly IHostEnvironment _environment;
    private readonly ILogger<JsonTrainingContentProvider> _logger;
    private readonly Lazy<IReadOnlyList<TrainingModuleContent>> _modules;

    public JsonTrainingContentProvider(IHostEnvironment environment, ILogger<JsonTrainingContentProvider> logger)
    {
        _environment = environment;
        _logger = logger;
        _modules = new Lazy<IReadOnlyList<TrainingModuleContent>>(LoadModules, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private IReadOnlyList<TrainingModuleContent> Modules => _modules.Value;

    public Task<IReadOnlyList<TrainingModuleContent>> GetLiveModulesAsync(CancellationToken cancellationToken = default)
    {
        var modules = Modules
            .Where(m => m.Live)
            .OrderBy(m => m.Position)
            .ToList();

        return Task.FromResult<IReadOnlyList<TrainingModuleContent>>(modules);
    }

    public Task<IReadOnlyList<TrainingModuleContent>> GetAllModulesAsync(CancellationToken cancellationToken = default)
    {
        var modules = Modules
            .OrderBy(m => m.Position)
            .ToList();

        return Task.FromResult<IReadOnlyList<TrainingModuleContent>>(modules);
    }

    public Task<TrainingModuleContent?> GetModuleByNameAsync(string moduleName, CancellationToken cancellationToken = default)
    {
        var module = Modules
            .FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(module);
    }

    public async Task<TrainingPageContent?> GetPageAsync(
        string moduleName,
        string pageName,
        CancellationToken cancellationToken = default)
    {
        var module = await GetModuleByNameAsync(moduleName, cancellationToken);
        return module?.PageByName(pageName);
    }

    private IReadOnlyList<TrainingModuleContent> LoadModules()
    {
        var path = ResolveContentPath();
        if (!File.Exists(path))
        {
            _logger.LogWarning("Demo training content not found at {Path}; using empty catalogue.", path);
            return [];
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<TrainingContentDocument>(json, JsonOptions)
            ?? new TrainingContentDocument();

        return document.Modules
            .OrderBy(m => m.Position)
            .Select(ToModule)
            .ToList();
    }

    private string ResolveContentPath()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "data", "demo-training-content.json")),
            Path.GetFullPath(Path.Combine(_environment.ContentRootPath, "..", "..", "..", "data", "demo-training-content.json")),
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static TrainingModuleContent ToModule(ModuleRecord record) => new(
        record.Name,
        record.Title,
        record.Description,
        record.Outcomes,
        record.Criteria,
        record.Duration,
        record.Position,
        record.Live,
        record.Pages.Select(ToPage).ToList(),
        record.Upcoming);

    private static TrainingPageContent ToPage(PageRecord record) => new(
        record.Name,
        record.PageType,
        record.Heading,
        record.Body,
        record.Answers.Select(a => new QuestionAnswerOption(a.Text, a.Correct)).ToList(),
        record.SuccessMessage,
        record.FailureMessage,
        record.Notes);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class TrainingContentDocument
    {
        public List<ModuleRecord> Modules { get; init; } = [];
    }

    private sealed record ModuleRecord
    {
        public string Name { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Outcomes { get; init; } = string.Empty;
        public string Criteria { get; init; } = string.Empty;
        public decimal Duration { get; init; }
        public int Position { get; init; }
        public bool Live { get; init; } = true;
        public string? Upcoming { get; init; }

        [JsonPropertyName("pages")]
        public List<PageRecord> Pages { get; init; } = [];
    }

    private sealed record PageRecord
    {
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("pageType")]
        public string PageType { get; init; } = string.Empty;

        public string Heading { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public List<AnswerRecord> Answers { get; init; } = [];
        public string? SuccessMessage { get; init; }
        public string? FailureMessage { get; init; }
        public bool Notes { get; init; }
    }

    private sealed record AnswerRecord
    {
        public string Text { get; init; } = string.Empty;
        public bool Correct { get; init; }
    }
}
