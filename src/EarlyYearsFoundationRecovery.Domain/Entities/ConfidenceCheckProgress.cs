namespace EarlyYearsFoundationRecovery.Domain.Entities;

public sealed class ConfidenceCheckProgress : ITimestamped
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public string CheckType { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? SkippedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public User User { get; set; } = null!;
}
