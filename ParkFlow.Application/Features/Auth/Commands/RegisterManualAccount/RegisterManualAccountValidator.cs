using FluentValidation;

namespace ParkFlow.Application.Features.Auth.Commands.RegisterManualAccount;

public class RegisterManualAccountValidator : AbstractValidator<RegisterManualAccountCommand>
{
    public RegisterManualAccountValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        When(x => !x.IsAdminCreated, () =>
        {
            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
                .MaximumLength(128).WithMessage("Password must not exceed 128 characters.")
                .Must(password => password == password?.Trim()).WithMessage("Password cannot start or end with spaces.")
                .Matches(@"[A-Z]").WithMessage("Password must contain an uppercase letter.")
                .Matches(@"[a-z]").WithMessage("Password must contain a lowercase letter.")
                .Matches(@"[0-9]").WithMessage("Password must contain a number.")
                .Matches(@"[!@#$%^&*()_+\-\=\[\]{};':""\\|,.<>\/?]")
                .WithMessage("Password must contain a special character.");
        });

        When(x => !string.IsNullOrWhiteSpace(x.FirstName), () =>
        {
            RuleFor(x => x.FirstName).MaximumLength(100);
        });

        When(x => !string.IsNullOrWhiteSpace(x.LastName), () =>
        {
            RuleFor(x => x.LastName).MaximumLength(100);
        });

        When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber), () =>
        {
            RuleFor(x => x.PhoneNumber).MaximumLength(20);
        });
    }
}
