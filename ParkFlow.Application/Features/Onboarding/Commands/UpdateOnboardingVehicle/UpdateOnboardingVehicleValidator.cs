using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;

public class UpdateOnboardingVehicleValidator : AbstractValidator<UpdateOnboardingVehicleCommand>
{
    public UpdateOnboardingVehicleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlateNumber).NotEmpty();
        RuleFor(x => x.Brand).NotEmpty();
        RuleFor(x => x.VehicleType)
            .Must(type => type is VehicleType.Motorcycle or VehicleType.Car)
            .WithMessage("Please select Motorcycle or Car.");
    }
}
