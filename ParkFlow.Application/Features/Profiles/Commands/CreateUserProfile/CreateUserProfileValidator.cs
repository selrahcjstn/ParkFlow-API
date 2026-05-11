using FluentValidation;

namespace ParkFlow.Application.Features.Profiles.Commands.CreateUserProfile;

public class CreateUserProfileValidator : AbstractValidator<CreateUserProfileCommand>
{
    public CreateUserProfileValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("First name is required.")
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.");

        RuleFor(x => x.IdCardNumber)
            .NotEmpty()
            .WithMessage("Id card number is required.")
            .MaximumLength(50)
            .WithMessage("Id card number must not exceed 50 characters.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Last name is required.")
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.");

        RuleFor(x => x.ProfilePictureUrl)
            .MaximumLength(2048)
            .WithMessage("Profile picture URL must not exceed 2048 characters.")
            .Must(BeAValidAbsoluteUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.ProfilePictureUrl))
            .WithMessage("Profile picture URL must be a valid absolute URL.");

        RuleFor(x => x.Course)
            .MaximumLength(100)
            .WithMessage("Course must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Course));

        RuleFor(x => x.Section)
            .MaximumLength(50)
            .WithMessage("Section must not exceed 50 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Section));

        RuleFor(x => x.YearLevel)
            .GreaterThan(0)
            .WithMessage("Year level must be greater than 0.")
            .When(x => x.YearLevel.HasValue);

        RuleFor(x => x.Office)
            .MaximumLength(100)
            .WithMessage("Office must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Office));
    }

    private static bool BeAValidAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}
