using Contentful.Core.Search;
using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace EarlyYearsFoundationRecovery.Infrastructure.ReferenceData;

public sealed class ContentfulReferenceDataProvider : IReferenceDataProvider
{
    private readonly ContentfulClientFactory _clientFactory;
    private readonly IContentfulContentCache _contentCache;
    private readonly ILogger<ContentfulReferenceDataProvider> _logger;

    public ContentfulReferenceDataProvider(
        ContentfulClientFactory clientFactory,
        IContentfulContentCache contentCache,
        ILogger<ContentfulReferenceDataProvider> logger)
    {
        _clientFactory = clientFactory;
        _contentCache = contentCache;
        _logger = logger;
    }

    public IReadOnlyList<ReferenceOption> Countries => GetSnapshot().Countries;
    public IReadOnlyList<SettingTypeOption> SettingTypes => GetSnapshot().SettingTypes;
    public IReadOnlyList<RoleOption> Roles => GetSnapshot().Roles;
    public IReadOnlyList<ReferenceOption> LocalAuthorities => GetSnapshot().LocalAuthorities;
    public IReadOnlyList<ReferenceOption> ExperienceLevels => GetSnapshot().ExperienceLevels;

    public ReferenceOption? GetCountry(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        GetSnapshot().Countries.ToDictionary(x => x.Id, StringComparer.Ordinal).TryGetValue(id, out var value)
            ? value
            : null;

    public SettingTypeOption? GetSettingType(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        GetSnapshot().SettingTypes.ToDictionary(x => x.Id, StringComparer.Ordinal).TryGetValue(id, out var value)
            ? value
            : null;

    public RoleOption? GetRole(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        GetSnapshot().Roles.ToDictionary(x => x.Id, StringComparer.Ordinal).TryGetValue(id, out var value)
            ? value
            : null;

    public ReferenceOption? GetLocalAuthority(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        GetSnapshot().LocalAuthorities.ToDictionary(x => x.Id, StringComparer.Ordinal).TryGetValue(id, out var value)
            ? value
            : null;

    public ReferenceOption? GetExperienceLevel(string? id) =>
        !string.IsNullOrWhiteSpace(id) &&
        GetSnapshot().ExperienceLevels.ToDictionary(x => x.Id, StringComparer.Ordinal).TryGetValue(id, out var value)
            ? value
            : null;

    public IReadOnlyList<RoleOption> GetRolesForGroup(string? group) =>
        GetSnapshot().Roles.Where(x => x.Group == group).ToList();

    private ReferenceDataSnapshot GetSnapshot() =>
        _contentCache.GetOrCreateAsync(
                ContentfulContentCache.ReferenceDataKey,
                cancellationToken => FetchReferenceDataAsync(_clientFactory, _logger, cancellationToken))
            .GetAwaiter()
            .GetResult();

    private static async Task<ReferenceDataSnapshot> FetchReferenceDataAsync(
        ContentfulClientFactory clientFactory,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        using var activity = ContentfulTelemetry.ActivitySource.StartActivity(
            "Contentful fetch reference data",
            System.Diagnostics.ActivityKind.Client);
        activity?.SetTag(
            "contentful.content_types",
            "userSetting,registrationRole,registrationCountry,registrationLocalAuthority,registrationExperience");
        activity?.SetTag("contentful.cache_key", ContentfulContentCache.ReferenceDataKey);

        try
        {
            var settings = await clientFactory.Client.GetEntries(
                QueryBuilder<UserSettingFields>.New.ContentTypeIs("userSetting").OrderBy("fields.name"),
                cancellationToken);
            var roles = await clientFactory.Client.GetEntries(
                QueryBuilder<RegistrationRoleFields>.New.ContentTypeIs("registrationRole"),
                cancellationToken);
            var countries = await clientFactory.Client.GetEntries(
                QueryBuilder<RegistrationCountryFields>.New.ContentTypeIs("registrationCountry"),
                cancellationToken);
            var localAuthorities = await clientFactory.Client.GetEntries(
                QueryBuilder<RegistrationLocalAuthorityFields>.New.ContentTypeIs("registrationLocalAuthority"),
                cancellationToken);
            var experienceLevels = await clientFactory.Client.GetEntries(
                QueryBuilder<RegistrationExperienceFields>.New.ContentTypeIs("registrationExperience"),
                cancellationToken);

            activity?.SetTag("contentful.result_count.user_settings", settings.Count());
            activity?.SetTag("contentful.result_count.roles", roles.Count());
            activity?.SetTag("contentful.result_count.countries", countries.Count());
            activity?.SetTag("contentful.result_count.local_authorities", localAuthorities.Count());
            activity?.SetTag("contentful.result_count.experience_levels", experienceLevels.Count());

            return new ReferenceDataSnapshot(
                countries.Select(ReferenceDataContentfulMapper.ToCountry).ToList(),
                ReferenceDataContentfulMapper.ToSettingTypes(settings).ToList(),
                roles.Select(ReferenceDataContentfulMapper.ToRole).ToList(),
                localAuthorities.Select(ReferenceDataContentfulMapper.ToLocalAuthority).ToList(),
                experienceLevels.Select(ReferenceDataContentfulMapper.ToExperienceLevel).ToList());
        }
        catch (Exception ex)
        {
            activity?.SetStatus(System.Diagnostics.ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Failed to load registration reference data from Contentful.");
            return new ReferenceDataSnapshot([], [], [], [], []);
        }
    }

    private sealed record ReferenceDataSnapshot(
        IReadOnlyList<ReferenceOption> Countries,
        IReadOnlyList<SettingTypeOption> SettingTypes,
        IReadOnlyList<RoleOption> Roles,
        IReadOnlyList<ReferenceOption> LocalAuthorities,
        IReadOnlyList<ReferenceOption> ExperienceLevels);
}

public static class ReferenceDataContentfulMapper
{
    public static IEnumerable<SettingTypeOption> ToSettingTypes(IEnumerable<UserSettingFields> settings)
    {
        foreach (var setting in settings.Where(setting => setting.Active ?? true))
        {
            if (string.IsNullOrWhiteSpace(setting.Name) || string.IsNullOrWhiteSpace(setting.Title))
            {
                continue;
            }

            yield return new SettingTypeOption(
                setting.Name,
                setting.Title,
                setting.LocalAuthority,
                string.IsNullOrWhiteSpace(setting.RoleType) ? "none" : setting.RoleType);
        }

        // Rails treats "other" as a virtual setting: link-only in the UI, but a
        // valid value for the free-text branch.
        yield return new SettingTypeOption("other", "Other", true, "other");
    }

    public static RoleOption ToRole(RegistrationRoleFields role) =>
        new(role.Name, role.Name, role.Group, role.HintText);

    public static ReferenceOption ToCountry(RegistrationCountryFields country) =>
        new(country.Id, country.Name);

    public static ReferenceOption ToLocalAuthority(RegistrationLocalAuthorityFields authority) =>
        new(authority.Name, authority.Name);

    public static ReferenceOption ToExperienceLevel(RegistrationExperienceFields experience) =>
        new(experience.Id, experience.Name);
}

public sealed class UserSettingFields
{
    public string Name { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    [JsonProperty("local_authority")]
    public bool LocalAuthority { get; set; }

    [JsonProperty("role_type")]
    public string RoleType { get; set; } = "none";

    public bool? Active { get; set; }
}

public sealed class RegistrationRoleFields
{
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;

    [JsonProperty("hint_text")]
    public string? HintText { get; set; }
}

public sealed class RegistrationCountryFields
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class RegistrationLocalAuthorityFields
{
    public string Name { get; set; } = string.Empty;
}

public sealed class RegistrationExperienceFields
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
