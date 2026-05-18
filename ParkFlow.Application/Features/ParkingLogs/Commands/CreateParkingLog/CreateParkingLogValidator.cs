using FluentValidation;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogValidator : AbstractValidator<CreateParkingLogCommand>
{
	public CreateParkingLogValidator()
	{
		RuleFor(x => x.QrCodeHash)
			.NotEmpty()
			.WithMessage("QrCodeHash is required.")
			.MaximumLength(200)
			.WithMessage("QrCodeHash is too long.");

		RuleFor(x => x.UserId)
			.NotEmpty()
			.WithMessage("UserId is required.");
	}
}
