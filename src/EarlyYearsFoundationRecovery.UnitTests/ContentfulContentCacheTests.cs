using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class ContentfulContentCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_runs_factory_once_for_concurrent_misses()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ContentfulContentCache(
            memoryCache,
            Options.Create(new ContentfulOptions { CacheMinutes = 5 }));
        var releaseFactory = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var factoryCalls = 0;

        var requests = Enumerable.Range(0, 10)
            .Select(_ => cache.GetOrCreateAsync(
                ContentfulContentCache.TrainingModulesKey,
                async _ =>
                {
                    Interlocked.Increment(ref factoryCalls);
                    await releaseFactory.Task;
                    return "training";
                }))
            .ToArray();

        await WaitUntilAsync(() => Volatile.Read(ref factoryCalls) == 1);
        Assert.Equal(1, Volatile.Read(ref factoryCalls));

        releaseFactory.SetResult();
        var results = await Task.WhenAll(requests);

        Assert.All(results, result => Assert.Equal("training", result));
        Assert.Equal(1, factoryCalls);
    }

    [Fact]
    public void InvalidateForContentType_clears_targeted_cache_entries()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ContentfulContentCache(
            memoryCache,
            Options.Create(new ContentfulOptions { CacheMinutes = 5 }));

        memoryCache.Set(ContentfulContentCache.TrainingModulesKey, "training");
        memoryCache.Set(ContentfulContentCache.FeedbackFormKey, "feedback");
        memoryCache.Set(ContentfulContentCache.StaticPagesKey, "static");
        memoryCache.Set(ContentfulContentCache.ReferenceDataKey, "reference");

        cache.InvalidateForContentType("page");

        Assert.False(memoryCache.TryGetValue(ContentfulContentCache.TrainingModulesKey, out _));
        Assert.True(memoryCache.TryGetValue(ContentfulContentCache.FeedbackFormKey, out _));
        Assert.True(memoryCache.TryGetValue(ContentfulContentCache.StaticPagesKey, out _));
        Assert.True(memoryCache.TryGetValue(ContentfulContentCache.ReferenceDataKey, out _));
    }

    [Fact]
    public void InvalidateForContentType_clears_reference_data_cache_entries()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cache = new ContentfulContentCache(
            memoryCache,
            Options.Create(new ContentfulOptions { CacheMinutes = 5 }));

        memoryCache.Set(ContentfulContentCache.TrainingModulesKey, "training");
        memoryCache.Set(ContentfulContentCache.ReferenceDataKey, "reference");

        cache.InvalidateForContentType("userSetting");

        Assert.True(memoryCache.TryGetValue(ContentfulContentCache.TrainingModulesKey, out _));
        Assert.False(memoryCache.TryGetValue(ContentfulContentCache.ReferenceDataKey, out _));
    }

    [Theory]
    [InlineData("{\"sys\":{\"contentType\":{\"sys\":{\"id\":\"static\"}}}}", "static")]
    [InlineData("{\"entity\":{\"sys\":{\"contentType\":{\"sys\":{\"id\":\"trainingModule\"}}}}}", "trainingModule")]
    public void TryGetContentTypeId_reads_content_type_from_payload(string payload, string expected)
    {
        var contentTypeId = ContentfulWebhookParser.TryGetContentTypeId(payload);
        Assert.Equal(expected, contentTypeId);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout.");
            }

            await Task.Delay(10);
        }
    }
}
