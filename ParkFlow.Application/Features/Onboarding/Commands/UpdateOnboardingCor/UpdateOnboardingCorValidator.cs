using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingCor;

public class UpdateOnboardingCorValidator : AbstractValidator<UpdateOnboardingCorCommand>
{
    public UpdateOnboardingCorValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.AcademicTerm).NotEmpty();
        RuleFor(x => x.CorDocumentUrl).NotEmpty();
    }
}
