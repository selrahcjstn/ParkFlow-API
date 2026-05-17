using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId;

public class GetVehiclesByOwnerIdHandler : IRequestHandler<GetVehiclesByOwnerIdQuery, Result<IEnumerable<VehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public GetVehiclesByOwnerIdHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<IEnumerable<VehicleDto>>> Handle(GetVehiclesByOwnerIdQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);

        var dtos = vehicles.Select(v => new VehicleDto(v.Id, v.PlateNumber, v.Brand, v.QrCodeHash, v.VehicleType));

        return Result<IEnumerable<VehicleDto>>.Success(dtos, "Vehicles retrieved.");
    }
}
