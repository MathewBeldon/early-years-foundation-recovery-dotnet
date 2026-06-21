using System.Text.Json;
using System.Text.Json.Serialization;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EarlyYearsFoundationRecovery.Infrastructure.ReferenceData;

public sealed class JsonReferenceDataProvider : IReferenceDataProvider
{
    private readonly ReferenceDataSnapshot _data;
    private readonly Dictionary<string, ReferenceOption> _countriesById;
    private readonly Dictionary<string, SettingTypeOption> _settingTypesById;
    private readonly Dictionary<string, RoleOption> _rolesById;
    private readonly Dictionary<string, ReferenceOption> _localAuthoritiesById;
    private readonly Dictionary<string, ReferenceOption> _experienceLevelsById;

    public JsonReferenceDataProvider(IHostEnvironment environment, ILogger<JsonReferenceDataProvider> logger)
    {
        var path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "data", "reference-data.json"));
        if (!File.Exists(path))
        {
            path = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "..", "..", "data", "reference-data.json"));
        }

        if (!File.Exists(path))
        {
            logger.LogWarning("Reference data file not found at {Path}; using empty defaults.", path);
            _data = new ReferenceDataSnapshot([], [], [], [], []);
        }
        else
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<ReferenceDataDocument>(json, JsonOptions)
                ?? throw new InvalidOperationException("Reference data file is invalid.");
            _data = document.ToSnapshot();
        }

        _countriesById = _data.Countries.ToDictionary(x => x.Id, StringComparer.Ordinal);
        _settingTypesById = _data.SettingTypes.ToDictionary(x => x.Id, StringComparer.Ordinal);
        _rolesById = _data.Roles.ToDictionary(x => x.Id, StringComparer.Ordinal);
        _localAuthoritiesById = _data.LocalAuthorities.ToDictionary(x => x.Id, StringComparer.Ordinal);
        _experienceLevelsById = _data.ExperienceLevels.ToDictionary(x => x.Id, StringComparer.Ordinal);
    }

    public IReadOnlyList<ReferenceOption> Countries => _data.Countries;
    public IReadOnlyList<SettingTypeOption> SettingTypes => _data.SettingTypes;
    public IReadOnlyList<RoleOption> Roles => _data.Roles;
    public IReadOnlyList<ReferenceOption> LocalAuthorities => _data.LocalAuthorities;
    public IReadOnlyList<ReferenceOption> ExperienceLevels => _data.ExperienceLevels;

    public ReferenceOption? GetCountry(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _countriesById.TryGetValue(id, out var value) ? value : null;

    public SettingTypeOption? GetSettingType(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _settingTypesById.TryGetValue(id, out var value) ? value : null;

    public RoleOption? GetRole(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _rolesById.TryGetValue(id, out var value) ? value : null;

    public ReferenceOption? GetLocalAuthority(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _localAuthoritiesById.TryGetValue(id, out var value) ? value : null;

    public ReferenceOption? GetExperienceLevel(string? id) =>
        !string.IsNullOrWhiteSpace(id) && _experienceLevelsById.TryGetValue(id, out var value) ? value : null;

    public IReadOnlyList<RoleOption> GetRolesForGroup(string? group) =>
        _data.Roles.Where(x => x.Group == group).ToList();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed record ReferenceDataSnapshot(
        IReadOnlyList<ReferenceOption> Countries,
        IReadOnlyList<SettingTypeOption> SettingTypes,
        IReadOnlyList<RoleOption> Roles,
        IReadOnlyList<ReferenceOption> LocalAuthorities,
        IReadOnlyList<ReferenceOption> ExperienceLevels);

    private sealed class ReferenceDataDocument
    {
        [JsonPropertyName("countries")]
        public List<ReferenceRecord> Countries { get; init; } = [];

        [JsonPropertyName("settingTypes")]
        public List<SettingTypeRecord> SettingTypes { get; init; } = [];

        [JsonPropertyName("roles")]
        public List<RoleRecord> Roles { get; init; } = [];

        [JsonPropertyName("localAuthorities")]
        public List<ReferenceRecord> LocalAuthorities { get; init; } = [];

        [JsonPropertyName("experienceLevels")]
        public List<ReferenceRecord> ExperienceLevels { get; init; } = [];

        public ReferenceDataSnapshot ToSnapshot() => new(
            Countries.Select(x => new ReferenceOption(x.Id, x.Label)).ToList(),
            SettingTypes.Select(x => new SettingTypeOption(x.Id, x.Label, x.RequiresLocalAuthority, x.RoleGroup)).ToList(),
            Roles.Select(x => new RoleOption(x.Id, x.Label, x.Group, x.Hint)).ToList(),
            LocalAuthorities.Select(x => new ReferenceOption(x.Id, x.Label)).ToList(),
            ExperienceLevels.Select(x => new ReferenceOption(x.Id, x.Label)).ToList());
    }

    private sealed record SettingTypeRecord(string Id, string Label, bool RequiresLocalAuthority, string RoleGroup);
    private sealed record RoleRecord(string Id, string Label, string Group, string? Hint = null);
    private sealed record ReferenceRecord(string Id, string Label);
}
