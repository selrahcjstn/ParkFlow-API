using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.ResetPasswordUserAccount;

public class ResetPasswordUserAccountValidator : AbstractValidator<ResetPasswordUserAccountCommand>
{
    public ResetPasswordUserAccountValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");

        RuleFor(x => x.ResetToken)
            .NotEmpty().WithMessage("Reset token is required")
            .MaximumLength(512).WithMessage("Reset token is too long");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long")
            .MaximumLength(128).WithMessage("Password must not exceed 128 characters")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit")
            .Matches(@"[!@#$%^&*()_+\-\=\[\]{};':""\\|,.<>\/?]")
            .WithMessage("Password must contain at least one special character");
    }
}
