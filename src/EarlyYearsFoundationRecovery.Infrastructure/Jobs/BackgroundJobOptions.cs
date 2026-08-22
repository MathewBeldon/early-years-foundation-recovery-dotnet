namespace EarlyYearsFoundationRecovery.Infrastructure.Jobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public TimeSpan LeaseTimeout { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromMinutes(1);

    internal static bool IsValid(BackgroundJobOptions options) =>
        options.LeaseTimeout > TimeSpan.Zero &&
        options.HeartbeatInterval > TimeSpan.Zero &&
        options.RecoveryInterval > TimeSpan.Zero &&
        options.HeartbeatInterval < options.LeaseTimeout / 2 &&
        options.RecoveryInterval < options.LeaseTimeout;
}
