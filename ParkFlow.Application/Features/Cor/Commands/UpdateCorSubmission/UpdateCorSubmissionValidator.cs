using FluentValidation;

namespace ParkFlow.Application.Features.Cor.Commands.UpdateCorSubmission;

public class UpdateCorSubmissionValidator : AbstractValidator<UpdateCorSubmissionCommand>
{
    public UpdateCorSubmissionValidator()
    {
        RuleFor(x => x.CorSubmissionId)
            .NotEmpty()
            .WithMessage("Cor submission id is required.");

        RuleFor(x => x.AcademicTerm)
            .MaximumLength(50)
            .WithMessage("Academic term must not exceed 50 characters.")
            .When(x => x.AcademicTerm != null);

        RuleFor(x => x.CorDocumentUrl)
            .MaximumLength(2048)
            .WithMessage("COR document URL is too long.")
            .Must(BeValidUrl)
            .When(x => !string.IsNullOrWhiteSpace(x.CorDocumentUrl))
            .WithMessage("COR document URL must be a valid URL.");

        RuleFor(x => x)
            .Must(HasAnyChange)
            .WithMessage("At least one field must be provided to update.");
    }

    private static bool HasAnyChange(UpdateCorSubmissionCommand cmd)
        => !(string.IsNullOrWhiteSpace(cmd.AcademicTerm)
             && string.IsNullOrWhiteSpace(cmd.CorDocumentUrl)
             && !cmd.VerificationStatus.HasValue);

    private static bool BeValidUrl(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out _);
}
