using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;

public class UpdateOnboardingScheduleValidator : AbstractValidator<UpdateOnboardingScheduleCommand>
{
    public UpdateOnboardingScheduleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty();
    }
}
