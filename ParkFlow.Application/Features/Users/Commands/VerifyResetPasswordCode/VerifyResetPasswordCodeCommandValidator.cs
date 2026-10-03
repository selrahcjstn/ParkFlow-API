using System.Linq;
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
            .Must(code =>
            {
                if (string.IsNullOrWhiteSpace(code)) return false;
                var trimmed = code.Trim();
                var digitCount = trimmed.Count(char.IsDigit);
                return digitCount == 6 || (trimmed.Length == 6 && !trimmed.Contains(' '));
            })
            .WithMessage("Verification code must be exactly 6 digits.");
    }
}
