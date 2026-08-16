using EarlyYearsFoundationRecovery.Domain.Entities;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

/// <summary>
/// Concrete mappings for Rails-owned columns where EF's snake-case convention
/// would select a different, still-existing legacy column.
/// </summary>
public sealed class RailsOwnedSchemaMappingTests
{
    [Fact]
    public void User_setting_type_maps_to_the_current_rails_setting_type_id_column()
    {
        using var context = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=localhost;Database=metadata_only")
                .UseSnakeCaseNamingConvention()
                .Options);

        var property = context.Model.FindEntityType(typeof(User))!
            .FindProperty(nameof(User.SettingType))!;

        Assert.Equal("setting_type_id", property.GetColumnName());
    }
}
