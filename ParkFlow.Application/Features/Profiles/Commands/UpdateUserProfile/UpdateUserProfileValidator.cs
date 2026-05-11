using FluentValidation;

namespace ParkFlow.Application.Features.Profiles.Commands.UpdateUserProfile;

public class UpdateUserProfileValidator : AbstractValidator<UpdateUserProfileCommand>
{
    public UpdateUserProfileValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.FirstName)
            .MaximumLength(100)
            .WithMessage("First name must not exceed 100 characters.")
            .When(x => x.FirstName != null);

        RuleFor(x => x.IdCardNumber)
            .NotEmpty()
            .WithMessage("Id card number cannot be empty.")
            .MaximumLength(50)
            .WithMessage("Id card number must not exceed 50 characters.")
            .When(x => x.IdCardNumber != null);

        RuleFor(x => x.LastName)
            .MaximumLength(100)
            .WithMessage("Last name must not exceed 100 characters.")
            .When(x => x.LastName != null);

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

        RuleFor(x => x.Department)
            .MaximumLength(100)
            .WithMessage("Department must not exceed 100 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Department));

        RuleFor(x => x)
            .Must(HasAnyChange)
            .WithMessage("At least one field must be provided to update.");
    }

    private static bool HasAnyChange(UpdateUserProfileCommand cmd)
           => !(string.IsNullOrWhiteSpace(cmd.IdCardNumber)
               && string.IsNullOrWhiteSpace(cmd.FirstName)
               && string.IsNullOrWhiteSpace(cmd.LastName)
               && string.IsNullOrWhiteSpace(cmd.ProfilePictureUrl)
               && string.IsNullOrWhiteSpace(cmd.Course)
               && string.IsNullOrWhiteSpace(cmd.Section)
               && !cmd.YearLevel.HasValue
               && string.IsNullOrWhiteSpace(cmd.Office)
               && string.IsNullOrWhiteSpace(cmd.Department));

    private static bool BeAValidAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}
