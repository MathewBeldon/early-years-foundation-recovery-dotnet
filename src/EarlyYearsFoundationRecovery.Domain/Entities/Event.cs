namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class Event
{
    public long Id { get; set; }
    public long? VisitId { get; set; }
    public long? UserId { get; set; }
    public string? Name { get; set; }
    public Dictionary<string, object?> Properties { get; set; } = [];
    public DateTime? Time { get; set; }

    public Visit? Visit { get; set; }
    public User? User { get; set; }
}
