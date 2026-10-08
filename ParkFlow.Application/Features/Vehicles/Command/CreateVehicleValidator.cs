using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class CreateVehicleValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleValidator()
    {
        RuleFor(x => x.VehicleType)
            .Must(type => type is VehicleType.Motorcycle or VehicleType.Car)
            .WithMessage("Please select Motorcycle or Car.");
        RuleFor(x => x.OwnerId)
            .NotEmpty()
            .WithMessage("OwnerId is required.");

        RuleFor(x => x.PlateNumber)
            .NotEmpty()
            .WithMessage("Plate number is required.")
            .MaximumLength(20)
            .WithMessage("Plate number must not exceed 20 characters.");

        RuleFor(x => x.Brand)
            .NotEmpty()
            .WithMessage("Brand is required.")
            .MaximumLength(100)
            .WithMessage("Brand must not exceed 100 characters.");
    }
}
