using FluentValidation;

namespace ParkFlow.Application.Features.Schedules.Command;

public class CreateParkingScheduleValidator : AbstractValidator<CreateParkingScheduleCommand>
{
	public CreateParkingScheduleValidator()
	{
		RuleFor(x => x.SubmissionId)
			.NotEmpty()
			.WithMessage("SubmissionId is required.");

		RuleFor(x => x.StartTime)
			.LessThan(x => x.EndTime)
			.WithMessage("StartTime must be before EndTime.");

		RuleFor(x => x.EndTime)
			.GreaterThan(x => x.StartTime)
			.WithMessage("EndTime must be after StartTime.");
	}
}
