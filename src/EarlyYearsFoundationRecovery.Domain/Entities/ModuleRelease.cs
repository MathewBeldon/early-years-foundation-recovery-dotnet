namespace EarlyYearsFoundationRecovery.Domain.Entities;

public sealed class ModuleRelease : ITimestamped
{
    public long Id { get; set; }
    public long ReleaseId { get; set; }
    public int ModulePosition { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime FirstPublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Release Release { get; set; } = null!;
}
