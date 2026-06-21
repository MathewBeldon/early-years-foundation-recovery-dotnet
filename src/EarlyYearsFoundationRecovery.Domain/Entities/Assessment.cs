namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class Assessment
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string TrainingModule { get; set; } = string.Empty;
    public float? Score { get; set; }
    public bool? Passed { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public User User { get; set; } = null!;
    public ICollection<Response> Responses { get; set; } = [];
}
