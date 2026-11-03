using FluentValidation;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;

public class UpdateOnboardingVehicleValidator : AbstractValidator<UpdateOnboardingVehicleCommand>
{
    public UpdateOnboardingVehicleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlateNumber)
            .NotEmpty()
            .When(x => x.VehicleType is VehicleType.Motorcycle or VehicleType.Car);
        RuleFor(x => x.Brand).NotEmpty();
        RuleFor(x => x.VehicleType).IsInEnum();
    }
}
