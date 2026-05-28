using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;

public class UpdateOnboardingVehicleValidator : AbstractValidator<UpdateOnboardingVehicleCommand>
{
    public UpdateOnboardingVehicleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlateNumber).NotEmpty();
        RuleFor(x => x.Brand).NotEmpty();
    }
}
