using System;
using System.Text.RegularExpressions;
using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;

public class UpdateOnboardingCorValidator : AbstractValidator<UpdateOnboardingCorCommand>
{
    public UpdateOnboardingCorValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AcademicTerm).NotEmpty().WithMessage("Academic term is required.");

        RuleFor(x => x.CorDocumentUrl)
            .Must(IsValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.CorDocumentUrl) && !string.Equals(x.CorDocumentUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            .WithMessage("COR document URL must be a valid uploaded file link.");

        RuleFor(x => x.OrcrDocumentUrl)
            .Must(IsValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.OrcrDocumentUrl) && !string.Equals(x.OrcrDocumentUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            .WithMessage("OR/CR document URL must be a valid uploaded file link.");

        RuleFor(x => x.MotorPictureUrl)
            .Must(IsValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.MotorPictureUrl) && !string.Equals(x.MotorPictureUrl.Trim(), "pending", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vehicle picture URL must be a valid uploaded file link.");
    }

    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        // Cloudinary's existing server-storage fallback returns this relative form.
        // Accept generated ParkFlow upload paths, not arbitrary relative paths.
        if (Regex.IsMatch(url, @"^/?uploads/parkflow/(cor|orcr|motor-pictures)/[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}\.(pdf|jpe?g|png|webp)$", RegexOptions.IgnoreCase))
            return true;
        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("content://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("ph://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase) ||
               Uri.TryCreate(url, UriKind.Absolute, out _);
    }
}
