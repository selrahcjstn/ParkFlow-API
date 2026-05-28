using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.MicrosoftAuthUserAccount;

public class MicrosoftAuthUserAccountValidator : AbstractValidator<MicrosoftAuthUserAccountCommand>
{
    public MicrosoftAuthUserAccountValidator()
    {
        RuleFor(x => x.ExternalProviderId)
            .NotEmpty().WithMessage("Microsoft user id is required.")
            .MaximumLength(200);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(256);
    }
}
