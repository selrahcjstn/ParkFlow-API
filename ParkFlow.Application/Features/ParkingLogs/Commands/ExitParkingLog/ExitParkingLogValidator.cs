using FluentValidation;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;

public class ExitParkingLogValidator : AbstractValidator<ExitParkingLogCommand>
{
    public ExitParkingLogValidator()
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