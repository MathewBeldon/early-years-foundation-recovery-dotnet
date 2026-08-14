namespace EarlyYearsFoundationRecovery.Domain.Entities;

public sealed class BackgroundJob : ITimestamped
{
    public long Id { get; set; }
    public string JobType { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public string Status { get; set; } = "queued";
    public int Attempts { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime RunAt { get; set; }
    public DateTime? LockedAt { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
