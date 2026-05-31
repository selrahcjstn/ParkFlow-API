using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.UpdateManualIdentity;

public class UpdateManualIdentityCommandValidator : AbstractValidator<UpdateManualIdentityCommand>
{
    public UpdateManualIdentityCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("New email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
