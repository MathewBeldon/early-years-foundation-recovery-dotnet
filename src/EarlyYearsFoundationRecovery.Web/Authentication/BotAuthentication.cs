using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace EarlyYearsFoundationRecovery.Web.Authentication;

public static class BotAuthentication
{
    public static bool SecretsMatch(string? provided, string? expected)
    {
        if (string.IsNullOrWhiteSpace(provided) || string.IsNullOrWhiteSpace(expected))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(expected));
    }
}

public sealed class BotAuthenticationFailureTracker(TimeProvider timeProvider)
{
    public const int MaximumFailedAttempts = 20;
    public static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, FailureWindowState> _failures = new();

    public bool RegisterFailure(string scope, string clientIp)
    {
        var state = _failures.GetOrAdd(CacheKey(scope, clientIp), static _ => new());
        var now = timeProvider.GetUtcNow();

        lock (state)
        {
            while (state.Attempts.TryPeek(out var attemptedAt) && now - attemptedAt >= FailureWindow)
            {
                state.Attempts.Dequeue();
            }

            if (state.Attempts.Count >= MaximumFailedAttempts)
            {
                return true;
            }

            state.Attempts.Enqueue(now);
            return false;
        }
    }

    public void AuthenticationSucceeded(string scope, string clientIp) =>
        _failures.TryRemove(CacheKey(scope, clientIp), out _);

    private static string CacheKey(string scope, string clientIp) => string.Concat(scope, ":", clientIp);

    private sealed class FailureWindowState
    {
        public Queue<DateTimeOffset> Attempts { get; } = new();
    }
}
