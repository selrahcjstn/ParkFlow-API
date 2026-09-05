using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Queries.GetAllReservations;

public class GetAllReservationsHandler : IRequestHandler<GetAllReservationsQuery, Result<IEnumerable<ParkingReservationDto>>>
{
    private readonly IParkingReservationRepository _reservationRepository;

    public GetAllReservationsHandler(IParkingReservationRepository reservationRepository)
    {
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<IEnumerable<ParkingReservationDto>>> Handle(GetAllReservationsQuery request, CancellationToken cancellationToken)
    {
        var reservations = await _reservationRepository.GetAllAsync(request.Status);

        var dtos = reservations.Select(r => new ParkingReservationDto
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
        });

        return Result<IEnumerable<ParkingReservationDto>>.Success(dtos, "All reservations retrieved successfully.");
    }
}
