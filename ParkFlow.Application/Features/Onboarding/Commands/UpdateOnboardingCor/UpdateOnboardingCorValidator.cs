using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;

public class UpdateOnboardingCorValidator : AbstractValidator<UpdateOnboardingCorCommand>
{
    public UpdateOnboardingCorValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
