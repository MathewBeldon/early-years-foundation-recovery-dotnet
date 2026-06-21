using System.Collections.Concurrent;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulContentCache(IMemoryCache cache, IOptions<ContentfulOptions> options)
    : IContentfulContentCache
{
    public const string TrainingModulesKey = "contentful-training-modules";
    public const string FeedbackFormKey = "contentful-feedback-form";
    public const string StaticPagesKey = "contentful-static-pages";
    public const string ReferenceDataKey = "contentful-reference-data";

    private readonly ConcurrentDictionary<string, SemaphoreSlim> _cacheLocks = new();

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(key, out T? cached))
        {
            return cached!;
        }

        var cacheLock = _cacheLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await cacheLock.WaitAsync(cancellationToken);

        try
        {
            if (cache.TryGetValue(key, out cached))
            {
                return cached!;
            }

            var value = await cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(options.Value.CacheMinutes);
                return await factory(cancellationToken);
            });

            return value!;
        }
        finally
        {
            cacheLock.Release();
        }
    }

    public void InvalidateAll()
    {
        cache.Remove(TrainingModulesKey);
        cache.Remove(FeedbackFormKey);
        cache.Remove(StaticPagesKey);
        cache.Remove(ReferenceDataKey);
    }

    public void InvalidateForContentType(string contentTypeId)
    {
        switch (contentTypeId)
        {
            case "page":
            case "trainingModule":
                InvalidateTraining();
                break;
            case "question":
            case "course":
                InvalidateFeedback();
                break;
            case "static":
                InvalidateStatic();
                break;
            case "userSetting":
            case "registrationRole":
            case "registrationCountry":
            case "registrationLocalAuthority":
            case "registrationExperience":
                InvalidateReferenceData();
                break;
            default:
                InvalidateAll();
                break;
        }
    }

    private void InvalidateTraining() => cache.Remove(TrainingModulesKey);

    private void InvalidateFeedback() => cache.Remove(FeedbackFormKey);

    private void InvalidateStatic() => cache.Remove(StaticPagesKey);

    private void InvalidateReferenceData() => cache.Remove(ReferenceDataKey);
}
