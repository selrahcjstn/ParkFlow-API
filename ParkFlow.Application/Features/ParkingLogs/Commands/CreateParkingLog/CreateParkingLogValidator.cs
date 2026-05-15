
using FluentValidation;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogValidator : AbstractValidator<CreateParkingLogCommand>
{
	public CreateParkingLogValidator()
	{
		RuleFor(x => x.VehicleId)
			.NotEmpty()
			.WithMessage("VehicleId is required.");

		RuleFor(x => x.GuardId)
			.NotEmpty()
			.WithMessage("GuardId is required.");

		RuleFor(x => x.EntryTime)
			.NotEmpty()
			.WithMessage("EntryTime is required.")
			.LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
			.WithMessage("EntryTime cannot be in the far future.");

		When(x => x.ExitTime.HasValue, () =>
		{
			RuleFor(x => x.ExitTime)
				.GreaterThanOrEqualTo(x => (DateTime?)x.EntryTime)
				.WithMessage("ExitTime must be the same or after EntryTime.");
		});
	}
}
