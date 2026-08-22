using EarlyYearsFoundationRecovery.Infrastructure.Jobs;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class BackgroundJobOptionsTests
{
    [Fact]
    public void Defaults_keep_heartbeat_and_recovery_well_inside_the_lease()
    {
        var options = new BackgroundJobOptions();

        Assert.True(BackgroundJobOptions.IsValid(options));
        Assert.Equal(TimeSpan.FromMinutes(15), options.LeaseTimeout);
        Assert.Equal(TimeSpan.FromMinutes(1), options.HeartbeatInterval);
        Assert.Equal(TimeSpan.FromMinutes(1), options.RecoveryInterval);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(15, 0, 1)]
    [InlineData(15, 1, 0)]
    [InlineData(15, 8, 1)]
    [InlineData(15, 1, 15)]
    public void Unsafe_intervals_are_rejected(int leaseMinutes, int heartbeatMinutes, int recoveryMinutes)
    {
        var options = new BackgroundJobOptions
        {
            LeaseTimeout = TimeSpan.FromMinutes(leaseMinutes),
            HeartbeatInterval = TimeSpan.FromMinutes(heartbeatMinutes),
            RecoveryInterval = TimeSpan.FromMinutes(recoveryMinutes),
        };

        Assert.False(BackgroundJobOptions.IsValid(options));
    }
}
