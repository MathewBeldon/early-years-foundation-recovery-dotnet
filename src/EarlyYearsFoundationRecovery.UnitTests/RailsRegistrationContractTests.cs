using EarlyYearsFoundationRecovery.Domain.Entities;

namespace EarlyYearsFoundationRecovery.UnitTests;

/// <summary>
/// Rails v1.5.0 registration predicates, pinned at commit
/// ac5467218a49c9de58a32a69d4edc01ce37710cf.
///
/// Rails uses registration_complete? for operational access/sign-in routing,
/// while registration_complete_any? is the OR predicate used for reporting and
/// analysis. These must remain separate so private-beta-only users are not
/// silently treated as public-registration-complete users.
/// </summary>
public sealed class RailsRegistrationContractTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void RegistrationCompleteAny_matches_rails_any_completion_truth_table(
        bool registrationComplete,
        bool privateBetaRegistrationComplete,
        bool expectedAnyCompletion)
    {
        var user = new User
        {
            RegistrationComplete = registrationComplete,
            PrivateBetaRegistrationComplete = privateBetaRegistrationComplete,
        };

        Assert.Equal(registrationComplete, user.RegistrationComplete);
        Assert.Equal(expectedAnyCompletion, user.RegistrationCompleteAny);
    }

    [Fact]
    public void Null_private_beta_completion_is_not_any_completion()
    {
        var user = new User
        {
            RegistrationComplete = false,
            PrivateBetaRegistrationComplete = null,
        };

        Assert.False(user.RegistrationCompleteAny);
    }

    [Fact]
    public void Operational_registration_predicate_remains_normal_completion()
    {
        // Rails application_controller.rb, link_helper.rb, and registration
        // controllers use registration_complete?, not registration_complete_any?.
        var privateBetaOnlyUser = new User
        {
            RegistrationComplete = false,
            PrivateBetaRegistrationComplete = true,
        };

        Assert.False(privateBetaOnlyUser.RegistrationComplete);
        Assert.True(privateBetaOnlyUser.RegistrationCompleteAny);
    }
}
