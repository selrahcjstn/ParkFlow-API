using FluentValidation;

namespace ParkFlow.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationValidator : AbstractValidator<CreateReservationCommand>
{
    public CreateReservationValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("UserId is required.");

        RuleFor(x => x.ReservationDate)
            .Must(date => date.Date > ParkFlow.Application.Features.ParkingLogs.Services.ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date)
            .WithMessage("Reservations must be booked at least 1 day in advance. Same-day reservations are not allowed.");

        RuleFor(x => x.EndTime)
            .LessThanOrEqualTo(TimeSpan.FromDays(1))
            .GreaterThan(x => x.StartTime)
            .WithMessage("End time must be after start time.");
        RuleFor(x => x.StartTime).GreaterThanOrEqualTo(TimeSpan.Zero).LessThan(TimeSpan.FromDays(1));
        RuleFor(x => x.Type).IsInEnum();

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(500).WithMessage("Reason must not exceed 500 characters.");
    }
}
