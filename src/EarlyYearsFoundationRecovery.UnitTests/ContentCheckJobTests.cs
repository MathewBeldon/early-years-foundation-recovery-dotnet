using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class ContentCheckJobTests
{
    [Fact]
    public async Task Invalid_modules_are_all_reported_and_job_completes()
    {
        var events = new List<string>();
        var cache = new TrackingCache(events);
        var check = new StubCheck(events,
        [
            new("module-1", false),
            new("module-2", true),
            new("module-3", false),
        ]);
        var logger = new RecordingLogger<ContentCheckJob>();
        var job = new ContentCheckJob(cache, check, logger);

        await job.RunAsync();

        Assert.Equal(["invalidate:trainingModule", "check"], events);
        Assert.Equal(2, logger.Entries.Count(entry => entry.Level == LogLevel.Warning));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("module-1", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("module-3", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("3 modules checked", StringComparison.Ordinal)
            && entry.Message.Contains("2 invalid", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Strict_fetch_failure_escapes_after_cache_invalidation()
    {
        var events = new List<string>();
        var expected = new InvalidOperationException("Contentful unavailable");
        var job = new ContentCheckJob(
            new TrackingCache(events),
            new StubCheck(events, expected),
            new RecordingLogger<ContentCheckJob>());

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => job.RunAsync());

        Assert.Same(expected, actual);
        Assert.Equal(["invalidate:trainingModule", "check"], events);
    }

    [Fact]
    public async Task Empty_strict_result_is_reported_but_never_created_from_configuration_fallback()
    {
        var events = new List<string>();
        var logger = new RecordingLogger<ContentCheckJob>();
        var job = new ContentCheckJob(
            new TrackingCache(events),
            new StubCheck(events, []),
            logger);

        await job.RunAsync();

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Information
            && entry.Message.Contains("0 modules checked", StringComparison.Ordinal));

        var unavailable = new UnavailableContentfulModuleIntegrityCheck();
        await Assert.ThrowsAsync<InvalidOperationException>(() => unavailable.CheckAllAsync());
    }

    private sealed class TrackingCache(List<string> events) : IContentfulContentCache
    {
        public Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default) =>
            factory(cancellationToken);

        public void InvalidateAll() => events.Add("invalidate:all");

        public void InvalidateForContentType(string contentTypeId) => events.Add($"invalidate:{contentTypeId}");
    }

    private sealed class StubCheck : IContentfulModuleIntegrityCheck
    {
        private readonly List<string> _events;
        private readonly IReadOnlyList<ContentfulModuleIntegrityResult>? _results;
        private readonly Exception? _exception;

        public StubCheck(List<string> events, IReadOnlyList<ContentfulModuleIntegrityResult> results)
        {
            _events = events;
            _results = results;
        }

        public StubCheck(List<string> events, Exception exception)
        {
            _events = events;
            _exception = exception;
        }

        public Task<IReadOnlyList<ContentfulModuleIntegrityResult>> CheckAllAsync(CancellationToken cancellationToken = default)
        {
            _events.Add("check");
            return _exception is null
                ? Task.FromResult(_results!)
                : Task.FromException<IReadOnlyList<ContentfulModuleIntegrityResult>>(_exception);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
