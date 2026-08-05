using System;
using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;

public class UpdateOnboardingCorValidator : AbstractValidator<UpdateOnboardingCorCommand>
{
    public UpdateOnboardingCorValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AcademicTerm).NotEmpty().WithMessage("Academic term is required.");

        RuleFor(x => x.CorDocumentUrl)
            .NotEmpty().WithMessage("COR / Student ID document is required.")
            .Must(url => !string.Equals(url?.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            .WithMessage("COR document must be uploaded before completing registration.")
            .Must(IsValidUrl)
            .WithMessage("COR document URL must be a valid uploaded file link.");

        RuleFor(x => x.OrcrDocumentUrl)
            .NotEmpty().WithMessage("OR/CR document is required.")
            .Must(url => !string.Equals(url?.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            .WithMessage("OR/CR document must be uploaded before completing registration.")
            .Must(IsValidUrl)
            .WithMessage("OR/CR document URL must be a valid uploaded file link.");

        RuleFor(x => x.MotorPictureUrl)
            .NotEmpty().WithMessage("Vehicle picture is required.")
            .Must(url => !string.Equals(url?.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vehicle picture must be uploaded before completing registration.")
            .Must(IsValidUrl)
            .WithMessage("Vehicle picture URL must be a valid uploaded file link.");
    }

    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }
}
