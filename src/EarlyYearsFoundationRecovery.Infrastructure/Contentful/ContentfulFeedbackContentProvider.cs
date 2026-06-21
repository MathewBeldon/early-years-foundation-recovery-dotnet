using Contentful.Core.Search;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulFeedbackContentProvider(
    ContentfulClientFactory clientFactory,
    IContentfulContentCache contentCache,
    ILogger<ContentfulFeedbackContentProvider> logger) : IFeedbackContentProvider
{
    public Task<FeedbackFormContent> GetFormAsync(CancellationToken cancellationToken = default) =>
        contentCache.GetOrCreateAsync(
            ContentfulContentCache.FeedbackFormKey,
            FetchFormAsync,
            cancellationToken);

    public async Task<FeedbackQuestionContent?> GetQuestionAsync(
        string questionName,
        CancellationToken cancellationToken = default)
    {
        var form = await GetFormAsync(cancellationToken);
        return form.PageByName(questionName);
    }

    private async Task<FeedbackFormContent> FetchFormAsync(CancellationToken cancellationToken)
    {
        using var activity = ContentfulTelemetry.ActivitySource.StartActivity(
            "Contentful fetch feedback form",
            System.Diagnostics.ActivityKind.Client);
        activity?.SetTag("contentful.content_type", "course");
        activity?.SetTag("contentful.cache_key", ContentfulContentCache.FeedbackFormKey);

        try
        {
            var builder = QueryBuilder<CourseFields>.New
                .ContentTypeIs("course")
                .Include(10)
                .Limit(1);

            var response = await clientFactory.Client.GetEntries(builder, cancellationToken);
            var course = response.FirstOrDefault();
            activity?.SetTag("contentful.result_count", course is null ? 0 : 1);
            if (course is null)
            {
                logger.LogWarning("No course entry found in Contentful.");
                return new FeedbackFormContent([]);
            }

            return ContentfulContentMapper.ToFeedbackForm(course.Feedback);
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to load feedback form from Contentful.");
            return new FeedbackFormContent([]);
        }
    }
}
