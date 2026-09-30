using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles;

public class GetAllVehiclesHandler : IRequestHandler<GetAllVehiclesQuery, Result<IEnumerable<AdminVehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;

    public GetAllVehiclesHandler(
        IVehicleRepository vehicleRepository,
        IParkingLogRepository parkingLogRepository,
        ICorSubmissionRepository corSubmissionRepository)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
        _corSubmissionRepository = corSubmissionRepository;
    }

    public async Task<Result<IEnumerable<AdminVehicleDto>>> Handle(GetAllVehiclesQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var latestCorByUser = corSubmissions
            .GroupBy(c => c.UserAccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAt).First());

        var dtos = new List<AdminVehicleDto>();

        foreach (var vehicle in vehicles)
        {
            var activeLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
            var status = activeLog != null ? activeLog.Status.ToString() : "Active";

            var ownerName = vehicle.Owner?.UserProfile != null
                ? $"{vehicle.Owner.UserProfile.FirstName} {vehicle.Owner.UserProfile.LastName}".Trim()
                : "Unassigned";

            var ownerEmail = vehicle.Owner?.PrimaryEmail ?? "N/A";
            var ownerRole = vehicle.Owner?.UserProfile?.Student != null
                ? "Student"
                : vehicle.Owner?.UserProfile?.Guard != null
                    ? "Guard"
                    : vehicle.Owner?.UserProfile?.Personnel != null
                        ? (vehicle.Owner.UserProfile.Personnel.Role == Roles.NonAcademicPersonnel ? "NonAcademicPersonnel" : "UniversityStaff")
                        : "Student";

            latestCorByUser.TryGetValue(vehicle.OwnerId, out var userCor);

            var orcrDocUrl = !string.IsNullOrWhiteSpace(vehicle.OrcrDocumentUrl)
                ? vehicle.OrcrDocumentUrl
                : userCor?.OrcrDocumentUrl;

            var vehiclePicUrl = !string.IsNullOrWhiteSpace(vehicle.VehiclePictureUrl)
                ? vehicle.VehiclePictureUrl
                : userCor?.MotorPictureUrl;

            dtos.Add(new AdminVehicleDto(
                vehicle.Id,
                vehicle.OwnerId,
                ownerName,
                ownerEmail,
                ownerRole,
                vehicle.PlateNumber,
                vehicle.Brand,
                vehicle.QrCodeHash,
                vehicle.VehicleType,
                status,
                vehicle.IsPrimary,
                orcrDocUrl,
                vehiclePicUrl,
                vehicle.VerificationStatus,
                vehicle.CreatedAt
            ));
        }

        return Result<IEnumerable<AdminVehicleDto>>.Success(dtos, "All vehicles retrieved.");
    }
}
