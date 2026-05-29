using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class DeleteVehicleHandler : IRequestHandler<DeleteVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public DeleteVehicleHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
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

        var wasPrimary = vehicle.IsPrimary;

        await _vehicleRepository.DeleteAsync(vehicle);

        // Promote another vehicle to primary if we deleted the primary one and they have others remaining
        if (wasPrimary)
        {
            var remainingVehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);
            var firstRemaining = remainingVehicles.FirstOrDefault();
            if (firstRemaining != null)
            {
                var trackedVehicle = await _vehicleRepository.GetByIdAsync(firstRemaining.Id);
                if (trackedVehicle != null)
                {
                    trackedVehicle.SetPrimary(true);
                    await _vehicleRepository.UpdateAsync(trackedVehicle);
                }
            }
        }

        return Result<Guid>.Success(request.VehicleId, "Vehicle successfully deleted.");
    }
}
