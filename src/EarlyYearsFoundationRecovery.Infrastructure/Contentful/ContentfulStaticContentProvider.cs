using Contentful.Core.Search;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulStaticContentProvider(
    ContentfulClientFactory clientFactory,
    IContentfulContentCache contentCache,
    ILogger<ContentfulStaticContentProvider> logger) : IStaticContentProvider
{
    public Task<IReadOnlyList<StaticPageContent>> GetPagesAsync(CancellationToken cancellationToken = default) =>
        contentCache.GetOrCreateAsync(
            ContentfulContentCache.StaticPagesKey,
            FetchPagesAsync,
            cancellationToken);

    public async Task<StaticPageContent?> GetPageByNameAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        var pages = await GetPagesAsync(cancellationToken);
        return pages.FirstOrDefault(page => string.Equals(page.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<StaticPageContent>> GetFooterPagesAsync(CancellationToken cancellationToken = default)
    {
        var pages = await GetPagesAsync(cancellationToken);
        return pages.Where(page => page.Footer).OrderBy(page => page.Heading).ToList();
    }

    private async Task<IReadOnlyList<StaticPageContent>> FetchPagesAsync(CancellationToken cancellationToken)
    {
        using var activity = ContentfulTelemetry.ActivitySource.StartActivity(
            "Contentful fetch static pages",
            System.Diagnostics.ActivityKind.Client);
        activity?.SetTag("contentful.content_type", "static");
        activity?.SetTag("contentful.cache_key", ContentfulContentCache.StaticPagesKey);

        try
        {
            var builder = QueryBuilder<StaticPageFields>.New
                .ContentTypeIs("static")
                .Limit(1000);

            var response = await clientFactory.Client.GetEntries(builder, cancellationToken);
            var pages = response
                .Select(ContentfulContentMapper.ToStaticPage)
                .ToList();
            activity?.SetTag("contentful.result_count", pages.Count);

            return pages;
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to load static pages from Contentful.");
            return [];
        }
    }
}
