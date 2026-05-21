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

        var vehicleDtos = await Task.WhenAll(vehicles.Select(async vehicle =>
        {
            var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
            var status = activeParkingLog?.Status.ToString() ?? ParkingStatus.Exited.ToString();

            return new VehicleDto(
                vehicle.Id,
                vehicle.PlateNumber,
                vehicle.Brand,
                vehicle.QrCodeHash,
                vehicle.VehicleType,
                status);
        }));

        return Result<IEnumerable<VehicleDto>>.Success(vehicleDtos, "Vehicles retrieved.");
    }
}
