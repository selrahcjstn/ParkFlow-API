using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles;

public class GetAllVehiclesHandler : IRequestHandler<GetAllVehiclesQuery, Result<IEnumerable<AdminVehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetAllVehiclesHandler(IVehicleRepository vehicleRepository, IParkingLogRepository parkingLogRepository)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<IEnumerable<AdminVehicleDto>>> Handle(GetAllVehiclesQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetAllAsync();
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
                : vehicle.Owner?.UserProfile?.Personnel != null
                    ? "UniversityStaff"
                    : "Student";

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
                vehicle.IsPrimary
            ));
        }

        return Result<IEnumerable<AdminVehicleDto>>.Success(dtos, "All vehicles retrieved.");
    }
}
