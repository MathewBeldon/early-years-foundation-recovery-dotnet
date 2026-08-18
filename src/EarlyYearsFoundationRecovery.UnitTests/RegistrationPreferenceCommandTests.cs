using EarlyYearsFoundationRecovery.Application.Registration.Commands;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class RegistrationPreferenceCommandTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Training_emails_validator_accepts_both_boolean_choices(bool value)
    {
        var result = await new UpdateTrainingEmailsCommandValidator()
            .ValidateAsync(new UpdateTrainingEmailsCommand(1, value));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Training_emails_validator_rejects_missing_choice()
    {
        var result = await new UpdateTrainingEmailsCommandValidator()
            .ValidateAsync(new UpdateTrainingEmailsCommand(1, null));

        Assert.False(result.IsValid);
        Assert.Equal("Choose an option.", Assert.Single(result.Errors).ErrorMessage);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Research_participant_validator_accepts_both_boolean_choices(bool value)
    {
        var result = await new UpdateResearchParticipantCommandValidator()
            .ValidateAsync(new UpdateResearchParticipantCommand(1, value));

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Research_participant_validator_rejects_missing_choice()
    {
        var result = await new UpdateResearchParticipantCommandValidator()
            .ValidateAsync(new UpdateResearchParticipantCommand(1, null));

        Assert.False(result.IsValid);
        Assert.Equal("Choose an option.", Assert.Single(result.Errors).ErrorMessage);
    }
}
