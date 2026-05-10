using FluentValidation;

namespace ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;

public class ForgotPasswordUserAccountValidator : AbstractValidator<ForgotPasswordUserAccountCommand>
{
    public ForgotPasswordUserAccountValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(255).WithMessage("Email must not exceed 255 characters");
    }
}
