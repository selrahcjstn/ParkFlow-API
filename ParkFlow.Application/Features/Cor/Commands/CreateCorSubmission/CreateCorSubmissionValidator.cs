using FluentValidation;

namespace ParkFlow.Application.Features.Cor.Commands.CreateCorSubmission;

public class CreateCorSubmissionValidator : AbstractValidator<CreateCorSubmissionCommand>
{
    public CreateCorSubmissionValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.AcademicTerm)
            .NotEmpty()
            .WithMessage("Academic term is required.")
            .MaximumLength(50)
            .WithMessage("Academic term must not exceed 50 characters.");

        RuleFor(x => x.CorDocumentUrl)
            .NotEmpty()
            .WithMessage("COR document URL is required.")
            .Must(BeValidUrl)
            .WithMessage("COR document URL must be a valid URL.")
            .MaximumLength(2048)
            .WithMessage("COR document URL is too long.");
    }

    private static bool BeValidUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out _);
}
