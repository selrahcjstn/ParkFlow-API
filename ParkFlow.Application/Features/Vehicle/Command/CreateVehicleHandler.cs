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
    private readonly IUserProfileRepository _userProfileRepository;

    public CreateVehicleHandler(
        IVehicleRepository vehicleRepository,
        IValidator<CreateVehicleCommand> validator,
        IQrCodeService qrCodeService, 
        IUserProfileRepository userProfileRepository)
    {
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _qrCodeService = qrCodeService;
        _userProfileRepository = userProfileRepository;
    }

    public async Task<Result<Guid>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }   

        var owner = await _userProfileRepository.GetByIdAsync(request.OwnerId);

        if (owner == null)
        {
            return Result<Guid>.Failure("Owner not found.", ErrorCode.NotFound);
        }

        var qrPayload = $"{owner.FirstName}:{owner.LastName}:{owner.Course}:{owner.YearLevel}:{owner.Section}:{owner.Office}:{request.PlateNumber}:{request.Brand}";
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
