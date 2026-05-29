using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class UpdateVehicleHandler : IRequestHandler<UpdateVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;

    public UpdateVehicleHandler(IVehicleRepository vehicleRepository)
    {
        _vehicleRepository = vehicleRepository;
    }

    public async Task<Result<Guid>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlateNumber))
            return Result<Guid>.Failure("Plate number is required.", ErrorCode.BadRequest);

        if (string.IsNullOrWhiteSpace(request.Brand))
            return Result<Guid>.Failure("Brand is required.", ErrorCode.BadRequest);

        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
        if (vehicle == null)
        {
            return Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound);
        }

        if (vehicle.OwnerId != request.OwnerId)
        {
            return Result<Guid>.Failure("Access Denied: You do not own this vehicle.", ErrorCode.Forbidden);
        }

        var existingVehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);
        var plateExists = existingVehicles.Any(v => v.Id != request.VehicleId && v.PlateNumber.Equals(request.PlateNumber, StringComparison.OrdinalIgnoreCase));
        if (plateExists)
        {
            return Result<Guid>.Failure("A vehicle with this plate number already exists.", ErrorCode.Conflict);
        }

        vehicle.Update(request.PlateNumber, request.Brand, request.VehicleType);

        await _vehicleRepository.UpdateAsync(vehicle);

        return Result<Guid>.Success(vehicle.Id, "Vehicle successfully updated.");
    }
}
