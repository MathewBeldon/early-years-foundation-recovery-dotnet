namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class UserModuleProgress : ITimestamped
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string ModuleName { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LastPage { get; set; }
    public Dictionary<string, bool> VisitedPages { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
