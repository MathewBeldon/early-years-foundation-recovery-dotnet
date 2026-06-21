namespace EarlyYearsFoundationRecovery.Application.Interfaces;

public interface IReferenceDataProvider
{
    IReadOnlyList<ReferenceOption> Countries { get; }
    IReadOnlyList<SettingTypeOption> SettingTypes { get; }
    IReadOnlyList<RoleOption> Roles { get; }
    IReadOnlyList<ReferenceOption> LocalAuthorities { get; }
    IReadOnlyList<ReferenceOption> ExperienceLevels { get; }

    ReferenceOption? GetCountry(string? id);
    SettingTypeOption? GetSettingType(string? id);
    RoleOption? GetRole(string? id);
    ReferenceOption? GetLocalAuthority(string? id);
    ReferenceOption? GetExperienceLevel(string? id);
    IReadOnlyList<RoleOption> GetRolesForGroup(string? group);
}

public sealed record ReferenceOption(string Id, string Label);

public sealed record SettingTypeOption(
    string Id,
    string Label,
    bool RequiresLocalAuthority,
    string RoleGroup);

public sealed record RoleOption(string Id, string Label, string Group, string? Hint = null);
