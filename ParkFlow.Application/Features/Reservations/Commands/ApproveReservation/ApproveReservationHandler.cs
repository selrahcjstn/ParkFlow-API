using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Commands.ApproveReservation;

public class ApproveReservationHandler : IRequestHandler<ApproveReservationCommand, Result<bool>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly ISignalRNotificationSender _notificationSender;

    public ApproveReservationHandler(
        IParkingReservationRepository reservationRepository,
        ISignalRNotificationSender notificationSender)
    {
        _reservationRepository = reservationRepository;
        _notificationSender = notificationSender;
    }

    public async Task<Result<bool>> Handle(ApproveReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId);
        if (reservation == null)
            return Result<bool>.Failure("Reservation not found.", ErrorCode.NotFound);

        try
        {
            reservation.Approve(request.AdminId, request.Notes);
            await _reservationRepository.UpdateAsync(reservation);
            await _reservationRepository.SaveChangesAsync();

            try
            {
                await _notificationSender.SendToAllAsync("ReservationUpdated", new { id = reservation.Id, status = "Approved" });
            }
            catch {}

            return Result<bool>.Success(true, "Reservation approved successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest);
        }
    }
}
