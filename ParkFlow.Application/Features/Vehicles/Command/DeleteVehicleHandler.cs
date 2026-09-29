using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class DeleteVehicleHandler : IRequestHandler<DeleteVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;
    private readonly INotificationService? _notificationService;

    public DeleteVehicleHandler(
        IVehicleRepository vehicleRepository,
        IParkingLogRepository parkingLogRepository,
        IUserAccountRepository? userAccountRepository = null,
        ISignalRNotificationSender? signalRNotificationSender = null,
        INotificationService? notificationService = null)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
        _userAccountRepository = userAccountRepository;
        _signalRNotificationSender = signalRNotificationSender;
        _notificationService = notificationService;
    }

    public async Task<Result<Guid>> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
        if (vehicle == null)
        {
            return Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound);
        }

        // If not admin, verify ownership
        if (!request.IsAdmin && vehicle.OwnerId != request.OwnerId)
        {
            return Result<Guid>.Failure("Access Denied: You do not own this vehicle.", ErrorCode.Forbidden);
        }

        // Regular users cannot delete their primary vehicle directly
        if (!request.IsAdmin && vehicle.IsPrimary)
        {
            return Result<Guid>.Failure("Cannot delete the primary vehicle. Please set another vehicle as primary first.", ErrorCode.BadRequest);
        }

        var activeParking = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
        if (activeParking != null)
        {
            return Result<Guid>.Failure("Cannot delete a vehicle with an active parking session.", ErrorCode.BadRequest);
        }

        var wasPrimary = vehicle.IsPrimary;
        var ownerId = vehicle.OwnerId;

        // Perform deletion
        await _vehicleRepository.DeleteAsync(vehicle);

        // Fetch remaining vehicles of this owner
        var remainingVehicles = (await _vehicleRepository.GetByOwnerIdAsync(ownerId))
            .Where(v => v.Id != vehicle.Id)
            .ToList();

        if (wasPrimary)
        {
            if (remainingVehicles.Any())
            {
                // Secondary vehicle exists: automatically promote it to primary
                var nextPrimary = remainingVehicles.FirstOrDefault(v => v.IsPrimary)
                    ?? remainingVehicles.OrderByDescending(v => v.VerificationStatus == CorVerificationStatus.Verified).First();

                nextPrimary.MarkAsPrimary();
                await _vehicleRepository.UpdateAsync(nextPrimary);

                if (_signalRNotificationSender != null)
                {
                    var eventData = new
                    {
                        userId = ownerId,
                        vehicleId = nextPrimary.Id,
                        plateNumber = nextPrimary.PlateNumber,
                        brand = nextPrimary.Brand,
                        type = "primary_vehicle_changed",
                        title = "Primary Vehicle Updated",
                        body = $"Vehicle {nextPrimary.PlateNumber} is now set as your primary parking vehicle."
                    };
                    try
                    {
                        await _signalRNotificationSender.SendToUserAsync(ownerId.ToString(), "VerificationStatusChanged", eventData);
                        await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", eventData);
                    }
                    catch { }
                }

                if (_notificationService != null)
                {
                    try
                    {
                        await _notificationService.CreateAndSendNotificationAsync(
                            ownerId,
                            "Primary Vehicle Updated",
                            $"Your vehicle {vehicle.PlateNumber} was deleted. Vehicle {nextPrimary.PlateNumber} has automatically been designated as your primary vehicle.",
                            type: "primary_vehicle_changed",
                            subtitle: "Primary Vehicle Updated",
                            actionRoute: "/(account)/vehicles",
                            actionText: "View Vehicles",
                            priority: "medium",
                            issuer: "ParkFlow Vehicle Desk"
                        );
                    }
                    catch { }
                }
            }
            else
            {
                // No remaining vehicles: user must upload/register vehicle again
                var user = _userAccountRepository != null ? await _userAccountRepository.GetByIdAsync(ownerId) : null;
                if (user != null && _userAccountRepository != null)
                {
                    user.ResetToVehicleStep();
                    await _userAccountRepository.UpdateAsync(user);

                    if (_signalRNotificationSender != null)
                    {
                        var eventData = new
                        {
                            userId = ownerId,
                            type = "vehicle_deleted",
                            title = "Vehicle Removed",
                            body = "Your primary vehicle was removed by an administrator. Please upload and register your vehicle again to enable parking access.",
                            requiresVehicleRegistration = true
                        };
                        try
                        {
                            await _signalRNotificationSender.SendToUserAsync(ownerId.ToString(), "VerificationStatusChanged", eventData);
                            await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", eventData);
                        }
                        catch { }
                    }

                    if (_notificationService != null)
                    {
                        try
                        {
                            await _notificationService.CreateAndSendNotificationAsync(
                                ownerId,
                                "Vehicle Removed",
                                "Your vehicle has been removed by an administrator. Please register your vehicle again to restore campus parking access.",
                                type: "vehicle_rejected",
                                subtitle: "Action Required",
                                actionRoute: "/(account)/vehicles",
                                actionText: "Add Vehicle",
                                priority: "high",
                                issuer: "ParkFlow Vehicle Desk"
                            );
                        }
                        catch { }
                    }
                }
            }
        }
        else
        {
            // Non-primary vehicle deleted
            if (_signalRNotificationSender != null)
            {
                try
                {
                    var eventData = new
                    {
                        userId = ownerId,
                        vehicleId = vehicle.Id,
                        type = "vehicle_deleted",
                        title = "Vehicle Removed",
                        body = $"Vehicle {vehicle.PlateNumber} was removed."
                    };
                    await _signalRNotificationSender.SendToUserAsync(ownerId.ToString(), "VerificationStatusChanged", eventData);
                    await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", eventData);
                }
                catch { }
            }
        }

        return Result<Guid>.Success(request.VehicleId, "Vehicle successfully deleted.");
    }
}
