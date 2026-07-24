using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId;

public class GetVehiclesByOwnerIdHandler : IRequestHandler<GetVehiclesByOwnerIdQuery, Result<IEnumerable<VehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetVehiclesByOwnerIdHandler(IVehicleRepository vehicleRepository, IParkingLogRepository parkingLogRepository)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<IEnumerable<VehicleDto>>> Handle(GetVehiclesByOwnerIdQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);

        var vehicleDtos = new List<VehicleDto>();

        foreach (var vehicle in vehicles)
        {
            var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
            var status = activeParkingLog?.Status.ToString() ?? ParkingStatus.Exited.ToString();

            vehicleDtos.Add(new VehicleDto(
                vehicle.Id,
                vehicle.PlateNumber,
                vehicle.Brand,
                vehicle.QrCodeHash,
                vehicle.VehicleType,
                status,
                vehicle.IsPrimary,
                vehicle.Color,
                vehicle.MotorPictureUrl,
                vehicle.OrcrDocumentUrl));
        }

        return Result<IEnumerable<VehicleDto>>.Success(vehicleDtos, "Vehicles retrieved.");
    }
}
