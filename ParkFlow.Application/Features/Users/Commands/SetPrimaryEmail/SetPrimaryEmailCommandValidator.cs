using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.SetPrimaryEmail;

public class SetPrimaryEmailCommandValidator : AbstractValidator<SetPrimaryEmailCommand>
{
    public SetPrimaryEmailCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
