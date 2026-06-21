namespace EarlyYearsFoundationRecovery.Domain.Entities;

public class Visit
{
    public long Id { get; set; }
    public string? VisitToken { get; set; }
    public string? VisitorToken { get; set; }
    public long? UserId { get; set; }
    public string? LandingPage { get; set; }
    public DateTime StartedAt { get; set; }

    public User? User { get; set; }
}
