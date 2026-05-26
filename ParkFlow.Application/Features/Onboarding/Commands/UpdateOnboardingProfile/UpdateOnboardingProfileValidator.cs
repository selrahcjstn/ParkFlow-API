using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingProfile;

public class UpdateOnboardingProfileValidator : AbstractValidator<UpdateOnboardingProfileCommand>
{
    public UpdateOnboardingProfileValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PhoneNumber).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.LastName).NotEmpty();
    }
}
