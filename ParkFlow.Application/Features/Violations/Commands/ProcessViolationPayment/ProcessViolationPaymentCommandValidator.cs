using FluentValidation;

namespace ParkFlow.Application.Features.Violations.Commands.ProcessViolationPayment;

public class ProcessViolationPaymentCommandValidator : AbstractValidator<ProcessViolationPaymentCommand>
{
    public ProcessViolationPaymentCommandValidator()
    {
        RuleFor(x => x.ReferenceNumber)
            .NotEmpty()
            .WithMessage("Reference number is required.");

        RuleFor(x => x.GuardUserId)
            .NotEmpty()
            .WithMessage("Guard user ID is required.");
    }
}
