using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogHandler : IRequestHandler<CreateParkingLogCommand, Result<Guid>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IGuardRepository _guardRepository;

    public CreateParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IGuardRepository guardRepository)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _guardRepository = guardRepository;
    }

    public async Task<Result<Guid>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        // Ensure vehicle exists
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
        if (vehicle == null)
            return Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound);

        // Ensure guard exists
        var guard = await _guardRepository.GetByUserProfileIdAsync(request.GuardId);
        if (guard == null)
            return Result<Guid>.Failure("Guard not found.", ErrorCode.NotFound);

        // If creating a parked entry
        if (request.Status == ParkingStatus.Parked)
        {
            var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(request.VehicleId);
            if (active != null)
            {
                return Result<Guid>.Failure("Vehicle is already parked.", ErrorCode.Conflict);
            }

            var parkingLog = new ParkingLog(vehicle, guard, request.EntryTime, ParkingStatus.Parked);
            await _parkingLogRepository.AddParkingLogAsync(parkingLog);

            return Result<Guid>.Success(parkingLog.Id, "Parking log created.");
        }

        // If exiting a parked vehicle
        if (request.Status == ParkingStatus.Exited)
        {
            var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(request.VehicleId);
            if (active == null)
            {
                return Result<Guid>.Failure("Active parking log not found for vehicle.", ErrorCode.NotFound);
            }

            var exitTime = request.ExitTime ?? DateTime.UtcNow;
            active.Exit(exitTime);
            await _parkingLogRepository.UpdateParkingLogAsync(active);

            return Result<Guid>.Success(active.Id, "Parking log exited.");
        }

        return Result<Guid>.Failure("Invalid parking status.", ErrorCode.BadRequest);
    }
}
