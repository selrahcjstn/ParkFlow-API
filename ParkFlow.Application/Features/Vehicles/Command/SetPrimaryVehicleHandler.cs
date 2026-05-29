using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class SetPrimaryVehicleHandler : IRequestHandler<SetPrimaryVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public SetPrimaryVehicleHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<Guid>> Handle(SetPrimaryVehicleCommand request, CancellationToken cancellationToken)
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
            return Result<Guid>.Success(vehicle.Id, "Vehicle is already primary.");
        }

        // Call domain method to set primary
        vehicle.MarkAsPrimary();

        // Retrieve other vehicles to toggle off their primary status
        var existingVehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);
        foreach (var other in existingVehicles.Where(v => v.Id != request.VehicleId && v.IsPrimary))
        {
            var trackedOther = await _vehicleRepository.GetByIdAsync(other.Id);
            if (trackedOther != null)
            {
                trackedOther.SetPrimary(false);
                await _vehicleRepository.UpdateAsync(trackedOther);
            }
        }

        await _vehicleRepository.UpdateAsync(vehicle);

        return Result<Guid>.Success(vehicle.Id, "Vehicle successfully set as primary.");
    }
}
