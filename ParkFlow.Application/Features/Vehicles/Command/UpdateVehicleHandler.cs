using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class UpdateVehicleHandler : IRequestHandler<UpdateVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository? _parkingLogRepository;
    private readonly IQrCodeService? _qrCodeService;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public UpdateVehicleHandler(
        IVehicleRepository vehicleRepository,
        IParkingLogRepository? parkingLogRepository = null,
        IQrCodeService? qrCodeService = null,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
        _qrCodeService = qrCodeService;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<Guid>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlateNumber))
            return Result<Guid>.Failure("Plate number is required.", ErrorCode.BadRequest);

        if (string.IsNullOrWhiteSpace(request.Brand))
            return Result<Guid>.Failure("Brand is required.", ErrorCode.BadRequest);

        if (_parkingLogRepository != null && await _parkingLogRepository.HasActiveParkingLogByUserIdAsync(request.OwnerId))
        {
            return Result<Guid>.Failure("Cannot update vehicle details while you have an ongoing parking session.", ErrorCode.BadRequest);
        }

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

        vehicle.Update(request.PlateNumber, request.Brand, request.VehicleType, request.OrcrDocumentUrl, request.VehiclePictureUrl);

        if (_qrCodeService != null)
        {
            var qrPayload = $"{request.OwnerId}:{request.PlateNumber}:{request.Brand}";
            var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
            var qrCodeHash = Convert.ToBase64String(SHA256.HashData(qrBytes));
            vehicle.Update(request.PlateNumber, request.Brand, request.VehicleType, qrCodeHash);
        }

        // Always put vehicle under review again when edited
        vehicle.UpdateVerificationStatus(CorVerificationStatus.Pending);

        await _vehicleRepository.UpdateAsync(vehicle);

        if (_signalRNotificationSender != null)
        {
            var eventData = new
            {
                userId = vehicle.OwnerId,
                vehicleId = vehicle.Id,
                type = "vehicle_pending",
                title = "Vehicle Verification Pending",
                body = "Your updated vehicle details are currently under review."
            };
            try
            {
                await _signalRNotificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "VerificationStatusChanged", eventData);
                await _signalRNotificationSender.SendToAllAsync("VerificationStatusChanged", eventData);
                await _signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "vehicle",
                    vehicleId = vehicle.Id
                });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        return Result<Guid>.Success(vehicle.Id, "Vehicle successfully updated and submitted for admin review.");
    }
}
