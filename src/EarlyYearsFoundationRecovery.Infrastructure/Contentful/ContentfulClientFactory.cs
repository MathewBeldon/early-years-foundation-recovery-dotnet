using Contentful.Core;
using Microsoft.Extensions.Options;
using SdkContentfulOptions = Contentful.Core.Configuration.ContentfulOptions;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<ContentfulOptions> options)
{
    private IContentfulClient? _client;

    public IContentfulClient Client => _client ??= CreateClient();

    private IContentfulClient CreateClient()
    {
        var settings = options.Value;
        var sdkOptions = new SdkContentfulOptions
        {
            DeliveryApiKey = settings.DeliveryApiKey,
            SpaceId = settings.SpaceId,
            Environment = settings.Environment,
        };

        return new ContentfulClient(
            httpClientFactory.CreateClient(nameof(ContentfulClientFactory)),
            sdkOptions);
    }
}
