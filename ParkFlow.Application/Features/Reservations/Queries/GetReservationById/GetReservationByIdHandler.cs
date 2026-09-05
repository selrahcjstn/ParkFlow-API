using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Queries.GetReservationById;

public class GetReservationByIdHandler : IRequestHandler<GetReservationByIdQuery, Result<ParkingReservationDto>>
{
    private readonly IParkingReservationRepository _reservationRepository;

    public GetReservationByIdHandler(IParkingReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<ParkingReservationDto>> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
    {
        var r = await _reservationRepository.GetByIdAsync(request.Id);
        if (r == null)
            return Result<ParkingReservationDto>.Failure("Reservation not found.", ErrorCode.NotFound);

        var dto = new ParkingReservationDto
        {
            Id = r.Id,
            UserId = r.UserId,
            UserFullName = r.UserAccount?.UserProfile != null ? $"{r.UserAccount.UserProfile.FirstName} {r.UserAccount.UserProfile.LastName}".Trim() : string.Empty,
            UserEmail = r.UserAccount?.PrimaryEmail ?? string.Empty,
            ReferenceNumber = r.ReferenceNumber,
            ReservationDate = r.ReservationDate,
            StartTime = r.StartTime,
            EndTime = r.EndTime,
            Reason = r.Reason,
            Status = r.Status,
            Type = r.Type,
            VehicleId = r.VehicleId,
            PlateNumber = r.Vehicle?.PlateNumber,
            Brand = r.Vehicle?.Brand,
            VehicleQrCodeHash = r.Vehicle?.QrCodeHash,
            AdminNotes = r.AdminNotes,
            ApprovedAt = r.ApprovedAt,
            ApprovedByAdminId = r.ApprovedByAdminId,
            CreatedAt = r.CreatedAt
        };

        return Result<ParkingReservationDto>.Success(dto, "Reservation details retrieved successfully.");
    }
}
