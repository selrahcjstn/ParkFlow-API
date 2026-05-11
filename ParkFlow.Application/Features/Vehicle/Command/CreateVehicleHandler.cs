using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using VehicleEntity = ParkFlow.Domain.Entities.Vehicle;

namespace ParkFlow.Application.Features.Vehicle.Command;

public class CreateVehicleHandler : IRequestHandler<CreateVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IValidator<CreateVehicleCommand> _validator;
    private readonly IQrCodeService _qrCodeService;

    public CreateVehicleHandler(
        IVehicleRepository vehicleRepository,
        IValidator<CreateVehicleCommand> validator,
        IQrCodeService qrCodeService)
    {
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _qrCodeService = qrCodeService;
    }

    public async Task<Result<Guid>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var qrPayload = $"{request.OwnerId}:{request.PlateNumber}:{request.Brand}";
        var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
        var qrCodeHash = HashQrBytes(qrBytes);

        var vehicle = new VehicleEntity(request.OwnerId, request.PlateNumber, request.Brand, qrCodeHash);

        await _vehicleRepository.AddAsync(vehicle);

        return Result<Guid>.Success(vehicle.Id, "Vehicle created.");
    }

    private static string HashQrBytes(byte[] bytes)
    {
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
