using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;

public class VerifyReservationScanHandler : IRequestHandler<VerifyReservationScanQuery, Result<VerifyReservationScanResponse>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;

    public VerifyReservationScanHandler(
        IParkingReservationRepository reservationRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository)
    {
        _reservationRepository = reservationRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<VerifyReservationScanResponse>> Handle(VerifyReservationScanQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QrCode))
            return Result<VerifyReservationScanResponse>.Failure("QR code input is empty.", ErrorCode.BadRequest);

        var trimmedQr = request.QrCode.Trim();

        ParkingReservation? reservation = await _reservationRepository.GetByReferenceNumberAsync(trimmedQr);
        Vehicle? vehicle = null;

        if (reservation != null)
        {
            if (reservation.VehicleId.HasValue)
            {
                vehicle = await _vehicleRepository.GetByIdAsync(reservation.VehicleId.Value);
            }
            else
            {
                var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(reservation.UserId);
                vehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();
            }
        }
        else
        {
            // Try looking up as Vehicle QR Code Hash
            vehicle = await _vehicleRepository.GetByQrCodeHashAsync(trimmedQr);
            if (vehicle != null)
            {
                var userReservations = await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId);
                var phToday = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow).Date;
                reservation = userReservations.FirstOrDefault(r => 
                    (r.VehicleId == vehicle.Id || r.VehicleId == null) &&
                    r.ReservationDate.Date == phToday &&
                    r.Status == ReservationStatus.Approved);

                if (reservation == null)
                {
                    reservation = userReservations.FirstOrDefault(r => 
                        (r.VehicleId == vehicle.Id || r.VehicleId == null) &&
                        r.ReservationDate.Date == phToday);
                }

                if (reservation == null)
                {
                    reservation = userReservations.FirstOrDefault();
                }
            }
        }

        if (reservation == null)
        {
            return Result<VerifyReservationScanResponse>.Failure("No reservation found for this QR code.", ErrorCode.NotFound);
        }

        var driverProfile = await _userProfileRepository.GetByUserIdAsync(reservation.UserId);
        var driverName = driverProfile != null ? $"{driverProfile.FirstName} {driverProfile.LastName}".Trim() : "Vehicle Owner";
        var driverEmail = reservation.UserAccount?.PrimaryEmail ?? string.Empty;

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var isToday = reservation.ReservationDate.Date == philippinesNow.Date;
        var isApproved = reservation.Status == ReservationStatus.Approved;

        bool isValid = isApproved && isToday;
        string statusMessage;

        if (!isApproved)
        {
            statusMessage = $"Reservation status is {reservation.Status}. Access not granted.";
        }
        else if (!isToday)
        {
            statusMessage = $"Reservation is for {reservation.ReservationDate:MMMM dd, yyyy}. Not valid today.";
        }
        else
        {
            statusMessage = reservation.Type == ReservationType.Special 
                ? "Verified Special Reservation Pass (₱0 / No Fees)" 
                : "Verified Standard Reservation Pass";
        }

        var response = new VerifyReservationScanResponse
        {
            IsValid = isValid,
            StatusMessage = statusMessage,
            ReservationId = reservation.Id,
            ReferenceNumber = reservation.ReferenceNumber,
            Type = reservation.Type,
            TypeName = reservation.Type == ReservationType.Special ? "Special (No Fees)" : "Normal",
            Status = reservation.Status,
            ReservationDate = reservation.ReservationDate,
            StartTime = reservation.StartTime.ToString(@"hh\:mm"),
            EndTime = reservation.EndTime.ToString(@"hh\:mm"),
            DriverName = driverName,
            DriverEmail = driverEmail,
            VehicleId = vehicle?.Id ?? reservation.VehicleId,
            PlateNumber = vehicle?.PlateNumber ?? reservation.Vehicle?.PlateNumber ?? "N/A",
            Brand = vehicle?.Brand ?? reservation.Vehicle?.Brand ?? "Vehicle",
            VehicleQrCodeHash = vehicle?.QrCodeHash ?? reservation.Vehicle?.QrCodeHash ?? string.Empty,
            Reason = reservation.Reason
        };

        return Result<VerifyReservationScanResponse>.Success(response, statusMessage);
    }
}
