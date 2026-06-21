using EarlyYearsFoundationRecovery.Application.Interfaces;
using EarlyYearsFoundationRecovery.Application.Registration;
using EarlyYearsFoundationRecovery.Domain.Entities;
using FluentValidation;
using MediatR;

namespace EarlyYearsFoundationRecovery.Application.Registration.Commands;

public sealed record UpdateTermsAndConditionsCommand(long UserId, bool Accepted) : IRequest<string>;

public sealed class UpdateTermsAndConditionsCommandValidator : AbstractValidator<UpdateTermsAndConditionsCommand>
{
    public UpdateTermsAndConditionsCommandValidator()
    {
        RuleFor(x => x.Accepted).Equal(true)
            .WithMessage("You must accept the terms and conditions and privacy policy to create an account.");
    }
}

public sealed class UpdateTermsAndConditionsCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateTermsAndConditionsCommand, string>
{
    public async Task<string> Handle(UpdateTermsAndConditionsCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.TermsAndConditionsAgreedAt = DateTime.UtcNow;
        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.StepPath(RegistrationJourney.Name);
    }
}

public sealed record UpdateNameCommand(long UserId, string FirstName, string LastName) : IRequest<string>;

public sealed class UpdateNameCommandValidator : AbstractValidator<UpdateNameCommand>
{
    public UpdateNameCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("Enter a first name.").MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().WithMessage("Enter a surname.").MaximumLength(100);
    }
}

public sealed class UpdateNameCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateNameCommand, string>
{
    public async Task<string> Handle(UpdateNameCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterName();
    }
}

public sealed record UpdateWhereYouLiveCommand(long UserId, string CountryId) : IRequest<string>;

public sealed class UpdateWhereYouLiveCommandValidator : AbstractValidator<UpdateWhereYouLiveCommand>
{
    public UpdateWhereYouLiveCommandValidator(IReferenceDataProvider referenceData)
    {
        RuleFor(x => x.CountryId)
            .NotEmpty()
            .WithMessage("Select where you live.")
            .Must(id => referenceData.GetCountry(id) is not null)
            .WithMessage("Select a valid country.");
    }
}

public sealed class UpdateWhereYouLiveCommandHandler(
    IUserRepository users,
    IReferenceDataProvider referenceData)
    : IRequestHandler<UpdateWhereYouLiveCommand, string>
{
    public async Task<string> Handle(UpdateWhereYouLiveCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var country = referenceData.GetCountry(request.CountryId)!;
        user.Country = country.Label;

        if (!RegistrationJourney.IsEngland(user))
        {
            user.LocalAuthority = null;
        }

        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterWhereYouLive();
    }
}

public sealed record UpdateSettingTypeCommand(long UserId, string SettingTypeId) : IRequest<string>;

public sealed class UpdateSettingTypeCommandValidator : AbstractValidator<UpdateSettingTypeCommand>
{
    public UpdateSettingTypeCommandValidator(IReferenceDataProvider referenceData)
    {
        RuleFor(x => x.SettingTypeId)
            .NotEmpty()
            .WithMessage("Enter the setting type you work in.")
            .Must(id => referenceData.GetSettingType(id) is not null)
            .WithMessage("Select a valid setting type.");
    }
}

public sealed class UpdateSettingTypeCommandHandler(
    IUserRepository users,
    IReferenceDataProvider referenceData)
    : IRequestHandler<UpdateSettingTypeCommand, string>
{
    public async Task<string> Handle(UpdateSettingTypeCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var settingType = referenceData.GetSettingType(request.SettingTypeId)!;
        user.SettingType = settingType.Id;
        RegistrationJourney.ApplySettingTypeReset(user, settingType);
        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterSettingType(user, settingType);
    }
}

public sealed record UpdateSettingTypeOtherCommand(long UserId, string SettingTypeOther) : IRequest<string>;

public sealed class UpdateSettingTypeOtherCommandValidator : AbstractValidator<UpdateSettingTypeOtherCommand>
{
    public UpdateSettingTypeOtherCommandValidator()
    {
        RuleFor(x => x.SettingTypeOther).NotEmpty().MaximumLength(255);
    }
}

public sealed class UpdateSettingTypeOtherCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateSettingTypeOtherCommand, string>
{
    public async Task<string> Handle(UpdateSettingTypeOtherCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.SettingType = "other";
        user.SettingTypeOther = request.SettingTypeOther.Trim();
        user.LocalAuthority = RegistrationJourney.NotApplicable;
        user.EarlyYearsExperience = null;
        user.RoleTypeOther = null;

        if (RegistrationJourney.IsEngland(user))
        {
            user.RoleType = RegistrationJourney.NotApplicable;
        }
        else
        {
            user.RoleType = null;
        }

        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterSettingTypeOther(user);
    }
}

public sealed record UpdateLocalAuthorityCommand(long UserId, string? LocalAuthorityId, bool Skip) : IRequest<string>;

public sealed class UpdateLocalAuthorityCommandValidator : AbstractValidator<UpdateLocalAuthorityCommand>
{
    public UpdateLocalAuthorityCommandValidator(IReferenceDataProvider referenceData)
    {
        RuleFor(x => x)
            .Must(x => x.Skip || !string.IsNullOrWhiteSpace(x.LocalAuthorityId))
            .WithMessage("Select a local authority or skip this step.");

        RuleFor(x => x.LocalAuthorityId)
            .Must(id => string.IsNullOrWhiteSpace(id) || referenceData.GetLocalAuthority(id) is not null)
            .WithMessage("Select a valid local authority.");
    }
}

public sealed class UpdateLocalAuthorityCommandHandler(
    IUserRepository users,
    IReferenceDataProvider referenceData)
    : IRequestHandler<UpdateLocalAuthorityCommand, string>
{
    public async Task<string> Handle(UpdateLocalAuthorityCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var settingType = referenceData.GetSettingType(user.SettingType)
            ?? throw new InvalidOperationException("Setting type not found.");

        user.LocalAuthority = request.Skip
            ? RegistrationJourney.MultipleLocalAuthorities
            : referenceData.GetLocalAuthority(request.LocalAuthorityId!)!.Label;

        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterLocalAuthority(user, settingType);
    }
}

public sealed record UpdateRoleTypeCommand(long UserId, string RoleTypeId) : IRequest<string>;

public sealed class UpdateRoleTypeCommandValidator : AbstractValidator<UpdateRoleTypeCommand>
{
    public UpdateRoleTypeCommandValidator(IReferenceDataProvider referenceData)
    {
        RuleFor(x => x.RoleTypeId)
            .NotEmpty()
            .WithMessage("Select your role.")
            .Must(id => referenceData.GetRole(id) is not null)
            .WithMessage("Select a valid role.");
    }
}

public sealed class UpdateRoleTypeCommandHandler(
    IUserRepository users,
    IReferenceDataProvider referenceData)
    : IRequestHandler<UpdateRoleTypeCommand, string>
{
    public async Task<string> Handle(UpdateRoleTypeCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var settingType = referenceData.GetSettingType(user.SettingType)
            ?? throw new InvalidOperationException("Setting type not found.");

        var role = referenceData.GetRole(request.RoleTypeId)!;
        user.RoleType = role.Id;
        user.RoleTypeOther = null;
        user.EarlyYearsExperience = null;
        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterRole(user, settingType, role.Id);
    }
}

public sealed record UpdateRoleTypeOtherCommand(long UserId, string RoleTypeOther) : IRequest<string>;

public sealed class UpdateRoleTypeOtherCommandValidator : AbstractValidator<UpdateRoleTypeOtherCommand>
{
    public UpdateRoleTypeOtherCommandValidator()
    {
        RuleFor(x => x.RoleTypeOther).NotEmpty().WithMessage("Enter your job title.").MaximumLength(255);
    }
}

public sealed class UpdateRoleTypeOtherCommandHandler(
    IUserRepository users,
    IReferenceDataProvider referenceData)
    : IRequestHandler<UpdateRoleTypeOtherCommand, string>
{
    public async Task<string> Handle(UpdateRoleTypeOtherCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        var settingType = referenceData.GetSettingType(user.SettingType)
            ?? throw new InvalidOperationException("Setting type not found.");

        user.RoleType = "other";
        user.RoleTypeOther = request.RoleTypeOther.Trim();
        user.EarlyYearsExperience = null;
        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterRoleOther(user, settingType);
    }
}

public sealed record UpdateEarlyYearsExperienceCommand(long UserId, string ExperienceId) : IRequest<string>;

public sealed class UpdateEarlyYearsExperienceCommandValidator : AbstractValidator<UpdateEarlyYearsExperienceCommand>
{
    public UpdateEarlyYearsExperienceCommandValidator(IReferenceDataProvider referenceData)
    {
        RuleFor(x => x.ExperienceId)
            .NotEmpty()
            .WithMessage("Choose an option.")
            .Must(id => referenceData.GetExperienceLevel(id) is not null)
            .WithMessage("Select a valid experience level.");
    }
}

public sealed class UpdateEarlyYearsExperienceCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateEarlyYearsExperienceCommand, string>
{
    public async Task<string> Handle(UpdateEarlyYearsExperienceCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.EarlyYearsExperience = request.ExperienceId;
        await users.SaveAsync(user, cancellationToken);

        return RegistrationJourney.NextStepAfterEarlyYearsExperience();
    }
}

public sealed record UpdateTrainingEmailsCommand(long UserId, bool? TrainingEmails) : IRequest<string>;

public sealed class UpdateTrainingEmailsCommandValidator : AbstractValidator<UpdateTrainingEmailsCommand>
{
    public UpdateTrainingEmailsCommandValidator()
    {
        RuleFor(x => x.TrainingEmails)
            .NotNull()
            .WithMessage("Choose an option.");
    }
}

public sealed class UpdateTrainingEmailsCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateTrainingEmailsCommand, string>
{
    public async Task<string> Handle(UpdateTrainingEmailsCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.TrainingEmails = request.TrainingEmails!.Value;
        await users.SaveAsync(user, cancellationToken);

        if (user.RegistrationComplete)
        {
            return "/my-account";
        }

        return RegistrationJourney.NextStepAfterTrainingEmails();
    }
}

public sealed record UpdateResearchParticipantCommand(long UserId, bool? ResearchParticipant) : IRequest<string>;

public sealed class UpdateResearchParticipantCommandValidator : AbstractValidator<UpdateResearchParticipantCommand>
{
    public UpdateResearchParticipantCommandValidator()
    {
        RuleFor(x => x.ResearchParticipant)
            .NotNull()
            .WithMessage("Choose an option.");
    }
}

public sealed class UpdateResearchParticipantCommandHandler(IUserRepository users)
    : IRequestHandler<UpdateResearchParticipantCommand, string>
{
    public async Task<string> Handle(UpdateResearchParticipantCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.ResearchParticipant = request.ResearchParticipant!.Value;
        await users.SaveAsync(user, cancellationToken);

        if (user.RegistrationComplete)
        {
            return "/my-account";
        }

        return RegistrationJourney.NextStepAfterResearchParticipant();
    }
}

public sealed record CompleteRegistrationCommand(long UserId) : IRequest<string>;

public sealed class CompleteRegistrationCommandHandler(IUserRepository users)
    : IRequestHandler<CompleteRegistrationCommand, string>
{
    public async Task<string> Handle(CompleteRegistrationCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new InvalidOperationException("User not found.");

        user.RegistrationComplete = true;
        await users.SaveAsync(user, cancellationToken);

        return PostSignInRedirect.ResolveAfterRegistrationComplete();
    }
}
