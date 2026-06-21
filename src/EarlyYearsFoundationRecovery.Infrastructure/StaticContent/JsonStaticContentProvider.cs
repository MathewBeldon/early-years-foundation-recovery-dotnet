using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.StaticContent;

public sealed class JsonStaticContentProvider(IHostEnvironment environment, ILogger<JsonStaticContentProvider> logger)
    : IStaticContentProvider
{
    private IReadOnlyList<StaticPageContent>? _pages;

    private IReadOnlyList<StaticPageContent> Pages => _pages ??= LoadPages();

    public Task<IReadOnlyList<StaticPageContent>> GetPagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Pages);

    public Task<StaticPageContent?> GetPageByNameAsync(string name, CancellationToken cancellationToken = default) =>
        Task.FromResult(Pages.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<StaticPageContent>> GetFooterPagesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StaticPageContent>>(Pages.Where(p => p.Footer).OrderBy(p => p.Heading).ToList());

    private IReadOnlyList<StaticPageContent> LoadPages()
    {
        var path = ResolveContentPath();
        if (!File.Exists(path))
        {
            logger.LogWarning("Demo static content not found at {Path}; using empty catalogue.", path);
            return [];
        }

        var json = File.ReadAllText(path);
        var document = JsonSerializer.Deserialize<StaticContentDocument>(json, JsonOptions)
            ?? new StaticContentDocument();

        return document.Pages.Select(ToPage).ToList();
    }

    private string ResolveContentPath()
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "data", "demo-static-pages.json")),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "..", "data", "demo-static-pages.json")),
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static StaticPageContent ToPage(PageRecord record) => new(
        record.Name,
        record.Title,
        record.Heading,
        record.Body,
        record.Footer,
        record.RequiresAuth);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class StaticContentDocument
    {
        public List<PageRecord> Pages { get; init; } = [];
    }

    private sealed record PageRecord
    {
        public string Name { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Heading { get; init; } = string.Empty;
        public string Body { get; init; } = string.Empty;
        public bool Footer { get; init; }
        public bool RequiresAuth { get; init; }
    }
}
