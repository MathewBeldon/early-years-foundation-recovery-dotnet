namespace EarlyYearsFoundationRecovery.Domain.Entities;

public sealed class Release
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object?> Properties { get; set; } = [];
    public DateTime Time { get; set; }
    public ICollection<ModuleRelease> Modules { get; set; } = [];
}
