using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Commands.CancelReservation;

public class CancelReservationHandler : IRequestHandler<CancelReservationCommand, Result<bool>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly ISignalRNotificationSender _notificationSender;

    private readonly TimeProvider _timeProvider;

    public CancelReservationHandler(
        IParkingReservationRepository reservationRepository,
        ISignalRNotificationSender notificationSender,
        TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        _reservationRepository = reservationRepository;
        _notificationSender = notificationSender;
    }

    public async Task<Result<bool>> Handle(CancelReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId);
        if (reservation == null)
            return Result<bool>.Failure("Reservation not found.", ErrorCode.NotFound);

        if (reservation.UserId != request.UserId)
            return Result<bool>.Failure("Unauthorized to cancel this reservation.", ErrorCode.Forbidden);

        if (reservation.Status == ReservationStatus.Approved)
        {
            var start = reservation.Type == ReservationType.Special ? TimeSpan.Zero : reservation.StartTime;
            var cutoff = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(reservation.ReservationDate, start);
            if (_timeProvider.GetUtcNow().UtcDateTime >= cutoff)
                return Result<bool>.Failure("This reservation is already valid for entry and can no longer be cancelled.", ErrorCode.BadRequest);
        }

        try
        {
            reservation.Cancel();
            await _reservationRepository.UpdateAsync(reservation);
            await _reservationRepository.SaveChangesAsync();

            try
            {
                await _notificationSender.SendToAllAsync("ReservationUpdated", new { id = reservation.Id, referenceNumber = reservation.ReferenceNumber, status = "Cancelled" });
                await _notificationSender.SendToAllAsync("ApprovalListUpdated", new { type = "Reservation", id = reservation.Id, referenceNumber = reservation.ReferenceNumber, status = "Cancelled" });
            }
            catch {}

            return Result<bool>.Success(true, "Reservation cancelled successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest);
        }
    }
}
