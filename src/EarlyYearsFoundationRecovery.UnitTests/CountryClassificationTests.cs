using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

public class CountryClassificationTests
{
    [Fact]
    public void ClassifyCountry_maps_whitespace_to_unknown()
    {
        var classification = RegistrationJourney.ClassifyCountry("   ");

        Assert.True(
            classification == CountryClassification.Unknown,
            "Whitespace must use the same unknown-country state as NULL.");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("England", true)]
    [InlineData("england", true)]
    [InlineData("Scotland", false)]
    public void JourneyRouting_matches_Rails_england_predicate(string? country, bool expected)
    {
        // Mirrors ac546721, app/controllers/registration/base_controller.rb:66-68.
        var actual = RegistrationJourney.IsEnglandForJourneyRouting(UserWithCountry(country));

        Assert.True(
            actual == expected,
            $"ac546721 base_controller.rb:66-68 expects {expected} for '{country ?? "<NULL>"}'.");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("England", true)]
    [InlineData("england", true)]
    [InlineData("Scotland", false)]
    public void SettingTypeTransition_matches_Rails_england_selected_predicates(string? country, bool expected)
    {
        // Mirrors ac546721, setting_types_controller.rb:44-46 and
        // setting_type_others_controller.rb:48-50.
        var actual = RegistrationJourney.IsEnglandForSettingTypeTransition(UserWithCountry(country));

        Assert.True(
            actual == expected,
            $"ac546721 setting type controllers expect {expected} for '{country ?? "<NULL>"}'.");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("England", true)]
    [InlineData("england", true)]
    [InlineData("Scotland", false)]
    public void Display_matches_Rails_authority_name_country_check(string? country, bool expected)
    {
        // Mirrors ac546721, app/models/user.rb:340-344.
        var actual = RegistrationJourney.IsEnglandForDisplay(UserWithCountry(country));

        Assert.True(
            actual == expected,
            $"ac546721 user.rb:340-344 expects {expected} for '{country ?? "<NULL>"}'.");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("England", true)]
    [InlineData("england", true)]
    [InlineData("Scotland", false)]
    public void SubmittedValue_matches_Rails_where_you_live_form(string? country, bool expected)
    {
        // Mirrors ac546721, app/forms/registration/where_you_live_form.rb:32-33.
        var actual = RegistrationJourney.IsEnglandSubmittedValue(country);

        Assert.True(
            actual == expected,
            $"ac546721 where_you_live_form.rb:32-33 expects {expected} for '{country ?? "<NULL>"}'.");
    }

    [Fact]
    public void AuthorityName_with_null_country_is_not_applicable()
    {
        // Regression for ac546721, app/models/user.rb:340-344: NULL is not England for display.
        var authorityName = UserProfileDisplay.AuthorityName(UserWithCountry(null));

        Assert.True(
            authorityName == RegistrationJourney.NotApplicable,
            $"Expected '{RegistrationJourney.NotApplicable}', but got '{authorityName}'.");
    }

    [Fact]
    public void ShowAuthority_with_null_country_is_false()
    {
        // RegistrationController ShowAuthority mirrors ac546721, app/models/user.rb:340-344.
        var user = UserWithCountry(null);
        var settingType = new SettingTypeOption("nursery", "Private nursery", true, "other");

        var showAuthority =
            RegistrationJourney.IsEnglandForDisplay(user) &&
            settingType.RequiresLocalAuthority &&
            !RegistrationJourney.IsNotApplicable(user.LocalAuthority);

        Assert.False(showAuthority);
    }

    private static User UserWithCountry(string? country) => new()
    {
        Country = country,
    };
}
