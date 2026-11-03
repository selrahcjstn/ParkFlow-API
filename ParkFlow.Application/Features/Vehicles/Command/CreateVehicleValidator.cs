using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class CreateVehicleValidator : AbstractValidator<CreateVehicleCommand>
{
    public CreateVehicleValidator()
    {
        RuleFor(x => x.VehicleType)
            .IsInEnum()
            .WithMessage("Invalid vehicle type.");

        RuleFor(x => x.OwnerId)
            .NotEmpty()
            .WithMessage("OwnerId is required.");

        RuleFor(x => x.PlateNumber)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Plate number is required.")
            .Must(p => !string.IsNullOrWhiteSpace(p))
            .WithMessage("Plate number is required.")
            .MaximumLength(20)
            .WithMessage("Plate number must not exceed 20 characters.")
            .When(x => x.VehicleType is VehicleType.Motorcycle or VehicleType.Car);

        RuleFor(x => x.PlateNumber)
            .MaximumLength(20)
            .WithMessage("Plate number must not exceed 20 characters.")
            .When(x => x.VehicleType == VehicleType.ElectricBike && !string.IsNullOrEmpty(x.PlateNumber));

        RuleFor(x => x.VehiclePictureUrl)
            .NotEmpty()
            .WithMessage("Vehicle photo is required for E-bike registration.")
            .When(x => x.VehicleType == VehicleType.ElectricBike);

        RuleFor(x => x.Brand)
            .NotEmpty()
            .WithMessage("Brand is required.")
            .MaximumLength(100)
            .WithMessage("Brand must not exceed 100 characters.");
    }
}
