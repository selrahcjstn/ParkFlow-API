using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Vehicles.Command;

public class CreateVehicleHandler : IRequestHandler<CreateVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IValidator<CreateVehicleCommand> _validator;
    private readonly IQrCodeService _qrCodeService;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public CreateVehicleHandler(
        IVehicleRepository vehicleRepository,
        IValidator<CreateVehicleCommand> validator,
        IQrCodeService qrCodeService,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _qrCodeService = qrCodeService;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<Guid>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }   

        var existingVehicles = await _vehicleRepository.GetByOwnerIdAsync(request.OwnerId);
        if (existingVehicles.Count() >= 5)
        {
            return Result<Guid>.Failure("Adding vehicle failed. A maximum of 5 vehicles are allowed per account.", ErrorCode.Conflict);
        }

        var qrPayload = $"{request.OwnerId}:{request.PlateNumber}:{request.Brand}";
        var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
        var qrCodeHash = HashQrBytes(qrBytes);

        var isPrimary = !existingVehicles.Any();
        var vehicle = new Vehicle(
            request.OwnerId,
            request.PlateNumber,
            request.Brand,
            qrCodeHash,
            request.VehicleType,
            request.OrcrDocumentUrl,
            request.VehiclePictureUrl,
            CorVerificationStatus.Pending);
        if (isPrimary)
        {
            vehicle.SetPrimary(true);
        }

        await _vehicleRepository.AddAsync(vehicle);

        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("VehicleSubmitted", new
                {
                    vehicleId = vehicle.Id,
                    ownerId = request.OwnerId,
                    plateNumber = request.PlateNumber,
                    brand = request.Brand,
                    vehicleType = request.VehicleType.ToString()
                });

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

        return Result<Guid>.Success(vehicle.Id, "Vehicle created.");
    }

    private static string HashQrBytes(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
