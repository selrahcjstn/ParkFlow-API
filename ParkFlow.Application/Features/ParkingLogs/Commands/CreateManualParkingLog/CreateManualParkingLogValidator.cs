using FluentValidation;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;

public class CreateManualParkingLogValidator : AbstractValidator<CreateManualParkingLogCommand>
{
    public CreateManualParkingLogValidator()
    {
        RuleFor(x => x.PlateNumber)
            .NotEmpty()
            .WithMessage("Plate number is required.")
            .MaximumLength(20)
            .WithMessage("Plate number must not exceed 20 characters.");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("UserId is required.");

        RuleFor(x => x.VehicleType)
            .IsInEnum()
            .WithMessage("Valid VehicleType is required.");
    }
}
