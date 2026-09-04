using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Commands.CancelReservation;

public class CancelReservationHandler : IRequestHandler<CancelReservationCommand, Result<bool>>
{
    private readonly IParkingReservationRepository _reservationRepository;

    public CancelReservationHandler(IParkingReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<bool>> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId);
        if (reservation == null)
            return Result<bool>.Failure("Reservation not found.", ErrorCode.NotFound);

        if (reservation.UserId != request.UserId)
            return Result<bool>.Failure("Unauthorized to cancel this reservation.", ErrorCode.Unauthorized);

        try
        {
            reservation.Cancel();
            await _reservationRepository.UpdateAsync(reservation);
            await _reservationRepository.SaveChangesAsync();
            return Result<bool>.Success(true, "Reservation cancelled successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest);
        }
    }
}
