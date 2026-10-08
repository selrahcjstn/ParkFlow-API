using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingPersonnel;

public class UpdateOnboardingPersonnelValidator : AbstractValidator<UpdateOnboardingPersonnelCommand>
{
    public UpdateOnboardingPersonnelValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.IdCardNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Department).NotEmpty();
    }
}
