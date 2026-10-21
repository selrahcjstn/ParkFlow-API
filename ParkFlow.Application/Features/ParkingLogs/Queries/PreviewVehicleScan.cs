using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.Services;
namespace ParkFlow.Application.Features.ParkingLogs.Queries;

public record VehicleScanPreview(string FullName, string Role, string? PhoneNumber, string? ProfilePictureUrl,
    string? VehiclePictureUrl, string PlateNumber, string Brand, string VehicleType, string QrCodeHash,
    bool IsCurrentlyParked, bool CanEnter, string? EntryRestriction);
public record PreviewVehicleScan(string QrCode) : IRequest<Result<VehicleScanPreview>>;
public class PreviewVehicleScanHandler(IVehicleRepository vehicles, IUserProfileRepository profiles,
    IParkingLogRepository logs, IParkingReservationRepository reservations) : IRequestHandler<PreviewVehicleScan, Result<VehicleScanPreview>>
{
    public async Task<Result<VehicleScanPreview>> Handle(PreviewVehicleScan r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.QrCode)) return Result<VehicleScanPreview>.Failure("Scan a parking pass.", ErrorCode.BadRequest);
        var vehicle = await vehicles.GetByQrCodeHashAsync(r.QrCode.Trim());
        if (vehicle == null)
        {
            var reservation = await reservations.GetByReferenceNumberAsync(r.QrCode.Trim());
            if (reservation?.VehicleId != null) vehicle = await vehicles.GetByIdAsync(reservation.VehicleId.Value);
            else if (reservation != null)
            {
                var pool = (await vehicles.GetByOwnerIdAsync(reservation.UserId)).Where(v =>
                    (v.VehicleType == ParkFlow.Domain.Enums.VehicleType.Car ? ParkFlow.Domain.Enums.VehicleType.Car : ParkFlow.Domain.Enums.VehicleType.Motorcycle) == reservation.ReservedVehicleType).ToList();
                vehicle = pool.FirstOrDefault(v => v.IsPrimary) ?? pool.FirstOrDefault();
            }
        }
        if (vehicle == null) return Result<VehicleScanPreview>.Failure("Vehicle not found for this pass.", ErrorCode.NotFound);
        var profile = await profiles.GetByUserIdAsync(vehicle.OwnerId);
        if (profile == null) return Result<VehicleScanPreview>.Failure("Driver profile not found.", ErrorCode.NotFound);
        var parked = await logs.GetActiveParkingLogByVehicleIdAsync(vehicle.Id) != null;
        var restriction = DriverEntryPolicy.MissingRequirement(profile);
        if (restriction == null && vehicle.VerificationStatus != ParkFlow.Domain.Enums.CorVerificationStatus.Verified)
            restriction = "Vehicle documents are waiting for approval or have been rejected.";
        var role = profile.Student != null ? "Student" : PersonnelParkingPolicy.IsFaculty(profile.Personnel) ? "Faculty"
            : profile.Personnel != null ? "University Staff" : profile.Guard != null ? "Guard" : "Admin";
        return Result<VehicleScanPreview>.Success(new($"{profile.FirstName} {profile.LastName}".Trim(), role,
            profile.UserAccount?.PhoneNumber, profile.ProfilePictureUrl, vehicle.VehiclePictureUrl,
            vehicle.PlateNumber, vehicle.Brand, vehicle.VehicleType.ToString(), vehicle.QrCodeHash, parked, restriction == null, restriction));
    }
}
