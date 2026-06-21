namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class Note : ITimestamped
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public string? TrainingModule { get; set; }
    public string? Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
