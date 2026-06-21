namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class Response : ITimestamped
{
    public long Id { get; set; }
    public long? UserId { get; set; }
    public string TrainingModule { get; set; } = string.Empty;
    public string QuestionName { get; set; } = string.Empty;
    public string? QuestionType { get; set; }
    public List<string> Answers { get; set; } = [];
    public bool? Correct { get; set; }
    public string? TextInput { get; set; }
    public long? AssessmentId { get; set; }
    public long? VisitId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User? User { get; set; }
    public Assessment? Assessment { get; set; }
}
