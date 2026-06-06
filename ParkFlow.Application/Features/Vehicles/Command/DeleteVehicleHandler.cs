using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class DeleteVehicleHandler : IRequestHandler<DeleteVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;

    public DeleteVehicleHandler(IVehicleRepository vehicleRepository, IParkingLogRepository parkingLogRepository)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<Guid>> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
        if (vehicle == null)
        {
            return Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound);
        }

        if (vehicle.OwnerId != request.OwnerId)
        {
            return Result<Guid>.Failure("Access Denied: You do not own this vehicle.", ErrorCode.Forbidden);
        }

        if (vehicle.IsPrimary)
        {
            return Result<Guid>.Failure("Cannot delete the primary vehicle. Please set another vehicle as primary first.", ErrorCode.BadRequest);
        }

        var activeParking = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
        if (activeParking != null)
        {
            return Result<Guid>.Failure("Cannot delete a vehicle with an active parking session.", ErrorCode.BadRequest);
        }

        await _vehicleRepository.DeleteAsync(vehicle);

        return Result<Guid>.Success(request.VehicleId, "Vehicle successfully deleted.");
    }
}
