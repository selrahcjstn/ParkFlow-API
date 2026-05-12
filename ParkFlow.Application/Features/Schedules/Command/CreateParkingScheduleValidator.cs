using FluentValidation;

namespace ParkFlow.Application.Features.Schedules.Command;

public class CreateParkingScheduleValidator : AbstractValidator<CreateParkingScheduleCommand>
{
    public CreateParkingScheduleValidator()
    {
        RuleFor(x => x.SubmissionId)
            .NotEmpty()
            .WithMessage("SubmissionId is required.");

        RuleFor(x => x.Schedules)
            .NotEmpty()
            .WithMessage("Schedules are required.");

        RuleForEach(x => x.Schedules).ChildRules(schedule =>
        {
            schedule.RuleFor(x => x.DayOfWeek)
                .IsInEnum()
                .WithMessage("Invalid day of week.");

            schedule.RuleFor(x => x.StartTime)
                .NotEmpty()
                .WithMessage("StartTime is required.");

            schedule.RuleFor(x => x.EndTime)
                .NotEmpty()
                .WithMessage("EndTime is required.");

            schedule.RuleFor(x => x)
                .Must(x => x.StartTime < x.EndTime)
                .WithMessage("StartTime must be before EndTime.");
        });
    }
}