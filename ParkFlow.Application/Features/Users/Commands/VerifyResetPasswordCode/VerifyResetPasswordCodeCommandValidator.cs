using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.VerifyResetPasswordCode;

public class VerifyResetPasswordCodeCommandValidator : AbstractValidator<VerifyResetPasswordCodeCommand>
{
    public VerifyResetPasswordCodeCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required.")
            .Length(6).WithMessage("Verification code must be exactly 6 digits.");
    }
}
