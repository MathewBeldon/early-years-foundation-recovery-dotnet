namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class MailEvent : ITimestamped
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Template { get; set; } = string.Empty;
    public Dictionary<string, object?> Personalisation { get; set; } = [];
    public Dictionary<string, object?>? Callback { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
