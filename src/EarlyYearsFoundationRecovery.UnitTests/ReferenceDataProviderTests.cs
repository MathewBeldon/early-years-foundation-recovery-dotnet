using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Infrastructure;
using EarlyYearsFoundationRecovery.Infrastructure.ReferenceData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class ReferenceDataProviderTests
{
    [Fact]
    public void AddInfrastructure_uses_json_reference_data_when_contentful_is_not_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddInfrastructure(configuration);

        var descriptor = services.Single(service => service.ServiceType == typeof(IReferenceDataProvider));
        Assert.Equal(typeof(JsonReferenceDataProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void AddInfrastructure_uses_contentful_reference_data_when_contentful_is_configured()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Contentful:SpaceId"] = "space",
                ["Contentful:DeliveryApiKey"] = "delivery-key",
            })
            .Build();

        services.AddInfrastructure(configuration);

        var descriptor = services.Single(service => service.ServiceType == typeof(IReferenceDataProvider));
        Assert.Equal(typeof(ContentfulReferenceDataProvider), descriptor.ImplementationType);
    }

    [Fact]
    public void Contentful_mapper_maps_original_user_setting_fields_and_adds_virtual_other()
    {
        var settings = ReferenceDataContentfulMapper.ToSettingTypes(
        [
            new UserSettingFields
            {
                Name = "nursery_private",
                Title = "Private nursery",
                LocalAuthority = true,
                RoleType = "other",
            },
            new UserSettingFields
            {
                Name = "inactive_setting",
                Title = "Inactive setting",
                LocalAuthority = true,
                RoleType = "other",
                Active = false,
            },
        ]).ToList();

        Assert.Contains(settings, setting =>
            setting.Id == "nursery_private" &&
            setting.Label == "Private nursery" &&
            setting.RequiresLocalAuthority &&
            setting.RoleGroup == "other");
        Assert.DoesNotContain(settings, setting => setting.Id == "inactive_setting");
        Assert.Contains(settings, setting =>
            setting.Id == "other" &&
            setting.Label == "Other" &&
            setting.RequiresLocalAuthority &&
            setting.RoleGroup == "other");
    }

    [Fact]
    public void Contentful_mapper_preserves_role_name_as_stored_value()
    {
        var role = ReferenceDataContentfulMapper.ToRole(new RegistrationRoleFields
        {
            Name = "Student",
            Group = "other",
            HintText = "Example hint",
        });

        Assert.Equal("Student", role.Id);
        Assert.Equal("Student", role.Label);
        Assert.Equal("other", role.Group);
        Assert.Equal("Example hint", role.Hint);
    }
}
