using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Commands.RejectReservation;

public class RejectReservationHandler : IRequestHandler<RejectReservationCommand, Result<bool>>
{
    private readonly IParkingReservationRepository _reservationRepository;

    public RejectReservationHandler(IParkingReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<bool>> Handle(RejectReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId);
        if (reservation == null)
            return Result<bool>.Failure("Reservation not found.", ErrorCode.NotFound);

        try
        {
            reservation.Reject(request.AdminId, request.Notes);
            await _reservationRepository.UpdateAsync(reservation);
            await _reservationRepository.SaveChangesAsync();
            return Result<bool>.Success(true, "Reservation rejected successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest);
        }
    }
}
