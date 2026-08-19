using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Notes;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

/// <summary>
/// Concrete mappings for Rails-owned columns where EF's snake-case convention
/// would select a different, still-existing legacy column.
/// </summary>
public sealed class RailsOwnedSchemaMappingTests
{
    // Rails v1.5.0, commit ac5467218a49c9de58a32a69d4edc01ce37710cf:
    // db/schema.rb users.private_beta_registration_complete.
    [Fact]
    public void User_private_beta_completion_maps_to_the_rails_column()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=metadata_only")
                .UseSnakeCaseNamingConvention()
                .Options,
            new RailsNoteBodyProtector(
                "schema-mapping-test-primary-key",
                "schema-mapping-test-salt"));

        var property = context.Model.FindEntityType(typeof(User))!
            .FindProperty(nameof(User.PrivateBetaRegistrationComplete))!;

        Assert.Equal("private_beta_registration_complete", property.GetColumnName());
        Assert.True(property.IsNullable);
    }

    [Fact]
    public void User_setting_type_id_and_snapshot_map_to_separate_Rails_columns()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=metadata_only")
                .UseSnakeCaseNamingConvention()
                .Options,
            new RailsNoteBodyProtector(
                "schema-mapping-test-primary-key",
                "schema-mapping-test-salt"));

        var entity = context.Model.FindEntityType(typeof(User))!;
        var identifier = entity.FindProperty(nameof(User.SettingTypeId))!;
        var snapshot = entity.FindProperty(nameof(User.SettingType))!;

        Assert.Equal("setting_type_id", identifier.GetColumnName());
        Assert.Equal("setting_type", snapshot.GetColumnName());
        Assert.True(identifier.IsNullable);
        Assert.True(snapshot.IsNullable);
    }
}
