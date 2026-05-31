using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.UpdateMicrosoftIdentity;

public class UpdateMicrosoftIdentityCommandValidator : AbstractValidator<UpdateMicrosoftIdentityCommand>
{
    public UpdateMicrosoftIdentityCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("New email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.NewExternalProviderId)
            .NotEmpty().WithMessage("New Microsoft Provider ID is required.");
    }
}
