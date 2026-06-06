using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.UnlinkIdentity;

public class UnlinkIdentityCommandValidator : AbstractValidator<UnlinkIdentityCommand>
{
    public UnlinkIdentityCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Provider)
            .IsInEnum().WithMessage("A valid authentication provider is required.");
    }
}
