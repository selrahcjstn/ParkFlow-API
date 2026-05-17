using FluentValidation;

namespace ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

public class CreateGuardAccountValidator : AbstractValidator<CreateGuardAccountCommand>
{
    public CreateGuardAccountValidator()
    {
        RuleFor(x => x.Account.Email)
            .NotEmpty()
            .WithMessage("Email is required.")
            .EmailAddress()
            .WithMessage("Email must be a valid email address.");

        RuleFor(x => x.Account.Password)
            .NotEmpty()
            .WithMessage("Password is required.")
            .MinimumLength(8)
            .WithMessage("Password must be at least 8 characters long.");

        RuleFor(x => x.Account.PhoneNumber)
            .NotEmpty()
            .WithMessage("Phone number is required.")
            .MaximumLength(20)
            .WithMessage("Phone number must not exceed 20 characters.");

        RuleFor(x => x.Profile.FirstName)
            .NotEmpty()
            .WithMessage("First name is required.")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.Profile.LastName)
            .NotEmpty()
            .WithMessage("Last name is required.")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.Profile.ProfilePictureUrl)
            .MaximumLength(2048)
            .WithMessage("Profile picture URL must not exceed 2048 characters.")
            .Must(BeAValidAbsoluteUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.Profile.ProfilePictureUrl))
            .WithMessage("Profile picture URL must be a valid absolute URL.");

        RuleFor(x => x.AssignedGate)
            .GreaterThan(0)
            .WithMessage("Assigned gate must be greater than 0.");
    }

    private static bool BeAValidAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}