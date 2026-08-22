namespace EarlyYearsFoundationRecovery.Domain.Entities;

public sealed class NewModuleNotificationDelivery : ITimestamped
{
    public long Id { get; set; }
    public long ModuleReleaseId { get; set; }
    public long UserId { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public int ModulePosition { get; set; }
    public string ModuleTitle { get; set; } = string.Empty;
    public string ModuleCriteria { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ModuleRelease ModuleRelease { get; set; } = null!;
    public User User { get; set; } = null!;
}
