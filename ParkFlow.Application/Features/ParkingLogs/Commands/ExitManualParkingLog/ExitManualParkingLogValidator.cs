using FluentValidation;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;

public class ExitManualParkingLogValidator : AbstractValidator<ExitManualParkingLogCommand>
{
    public ExitManualParkingLogValidator()
    {
        RuleFor(x => x.PlateNumber)
            .NotEmpty()
            .WithMessage("Plate number is required.")
            .MaximumLength(50)
            .WithMessage("Plate number is too long.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");
    }
}
