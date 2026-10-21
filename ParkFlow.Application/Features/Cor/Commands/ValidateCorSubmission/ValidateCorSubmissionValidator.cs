using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Cor.Commands.ValidateCorSubmission;

public class ValidateCorSubmissionValidator : AbstractValidator<ValidateCorSubmissionCommand>
{
    public ValidateCorSubmissionValidator()
    {
        RuleFor(x => x.CorSubmissionId)
            .NotEmpty()
            .WithMessage("COR submission ID is required.");

        RuleFor(x => x.VerificationStatus)
            .IsInEnum()
            .WithMessage("Verification status must be a valid value.")
            .Must(status => status is CorVerificationStatus.Verified or CorVerificationStatus.Rejected)
            .WithMessage("Manual review must approve or reject the document.");
    }
}
