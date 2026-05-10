using FluentValidation;

namespace ParkFlow.Application.Features.Profiles.Commands;

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

        RuleFor(x => x)
            .Must(HasAnyChange)
            .WithMessage("At least one field must be provided to update.");
    }

    private static bool HasAnyChange(UpdateUserProfileCommand cmd)
        => !(string.IsNullOrWhiteSpace(cmd.FirstName)
             && string.IsNullOrWhiteSpace(cmd.LastName)
             && string.IsNullOrWhiteSpace(cmd.ProfilePictureUrl));

    private static bool BeAValidAbsoluteUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}
