using FluentValidation;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingSchedule;

public class UpdateOnboardingScheduleValidator : AbstractValidator<UpdateOnboardingScheduleCommand>
{
    public UpdateOnboardingScheduleValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().Must(items => items != null &&
            ParkFlow.Application.Features.Verification.CorTextVerification.CheckSchedules(items.Select(i =>
                new ParkFlow.Application.Interfaces.VerificationSchedule(i.DayOfWeek, i.StartTime, i.EndTime))).Count == 0)
            .WithMessage("Schedules must use valid days and times without overlaps or duplicates.");
    }
}
