using FluentValidation;

namespace ParkFlow.Application.Features.Cor.Commands.DeleteCorSubmission;

public class DeleteCorSubmissionValidator : AbstractValidator<DeleteCorSubmissionCommand>
{
    public DeleteCorSubmissionValidator()
    {
        RuleFor(x => x.CorSubmissionId)
            .NotEmpty()
            .WithMessage("Cor submission id is required.");
    }
}
