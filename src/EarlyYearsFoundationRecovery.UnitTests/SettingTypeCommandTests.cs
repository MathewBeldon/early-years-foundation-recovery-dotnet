using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Application.Registration.Commands;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class SettingTypeCommandTests
{
    private static readonly TestReferenceData ReferenceData = new();

    [Fact]
    public async Task Normal_selection_saves_identifier_current_title_and_clears_other_together()
    {
        var user = ExistingUser();
        user.SettingTypeId = "other";
        user.SettingType = "other";
        user.SettingTypeOther = "Old custom setting";
        var repository = new RecordingUserRepository(user);
        var handler = new UpdateSettingTypeCommandHandler(repository, ReferenceData);

        await handler.Handle(new UpdateSettingTypeCommand(user.Id, "nursery"), CancellationToken.None);

        var saved = Assert.Single(repository.Saves);
        Assert.Equal(("nursery", "Current private nursery title", null), saved);
        Assert.Equal("nursery", user.SettingTypeId);
        Assert.Equal("Current private nursery title", user.SettingType);
        Assert.Null(user.SettingTypeOther);
    }

    [Fact]
    public async Task Other_selection_saves_canonical_other_values_and_clears_old_custom_text()
    {
        var user = ExistingUser();
        user.SettingTypeOther = "Old custom setting";
        var repository = new RecordingUserRepository(user);
        var handler = new UpdateSettingTypeCommandHandler(repository, ReferenceData);

        var next = await handler.Handle(
            new UpdateSettingTypeCommand(user.Id, "other"),
            CancellationToken.None);

        var saved = Assert.Single(repository.Saves);
        Assert.Equal(("other", "other", null), saved);
        Assert.Equal(RegistrationJourney.StepPath(RegistrationJourney.SettingTypeOther), next);
    }

    [Fact]
    public async Task Other_text_step_saves_both_canonical_values_with_trimmed_custom_text()
    {
        var user = ExistingUser();
        var repository = new RecordingUserRepository(user);
        var handler = new UpdateSettingTypeOtherCommandHandler(repository);

        await handler.Handle(
            new UpdateSettingTypeOtherCommand(user.Id, "  Forest school  "),
            CancellationToken.None);

        var saved = Assert.Single(repository.Saves);
        Assert.Equal(("other", "other", "Forest school"), saved);
        Assert.Equal(RegistrationJourney.NotApplicable, user.LocalAuthority);
        Assert.Equal(RegistrationJourney.NotApplicable, user.RoleType);
    }

    private static User ExistingUser() => new()
    {
        Id = 42,
        Country = "England",
        SettingTypeId = "nursery",
        SettingType = "Old private nursery title",
    };

    private sealed class RecordingUserRepository(User user) : IUserRepository
    {
        public List<(string? Id, string? Snapshot, string? Other)> Saves { get; } = [];

        public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult<User?>(id == user.Id ? user : null);

        public Task<User?> GetByGovOneIdAsync(string govOneId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<User> FindOrCreateFromGovOneAsync(
            string email,
            string govOneId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveAsync(User savedUser, CancellationToken cancellationToken = default)
        {
            Saves.Add((savedUser.SettingTypeId, savedUser.SettingType, savedUser.SettingTypeOther));
            return Task.CompletedTask;
        }
    }

    private sealed class TestReferenceData : IReferenceDataProvider
    {
        public IReadOnlyList<ReferenceOption> Countries { get; } = [];
        public IReadOnlyList<SettingTypeOption> SettingTypes { get; } =
        [
            new("nursery", "Current private nursery title", true, "other"),
            new("other", "Other", true, "other"),
        ];

        public IReadOnlyList<RoleOption> Roles { get; } = [];
        public IReadOnlyList<ReferenceOption> LocalAuthorities { get; } = [];
        public IReadOnlyList<ReferenceOption> ExperienceLevels { get; } = [];

        public ReferenceOption? GetCountry(string? id) => null;
        public SettingTypeOption? GetSettingType(string? id) =>
            SettingTypes.FirstOrDefault(option => option.Id == id);

        public RoleOption? GetRole(string? id) => null;
        public ReferenceOption? GetLocalAuthority(string? id) => null;
        public ReferenceOption? GetExperienceLevel(string? id) => null;
        public IReadOnlyList<RoleOption> GetRolesForGroup(string? group) => [];
    }
}
