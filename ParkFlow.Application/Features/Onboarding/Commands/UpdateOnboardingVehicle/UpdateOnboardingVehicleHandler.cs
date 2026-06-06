using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Onboarding.Commands.UpdateOnboardingVehicle;

public class UpdateOnboardingVehicleHandler : IRequestHandler<UpdateOnboardingVehicleCommand, Result<Guid>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IQrCodeService _qrCodeService;
    private readonly IValidator<UpdateOnboardingVehicleCommand> _validator;

    public UpdateOnboardingVehicleHandler(
        IVehicleRepository vehicleRepository,
        IUserAccountRepository userAccountRepository,
        IQrCodeService qrCodeService,
        IValidator<UpdateOnboardingVehicleCommand> validator)
    {
        _vehicleRepository = vehicleRepository;
        _userAccountRepository = userAccountRepository;
        _qrCodeService = qrCodeService;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(UpdateOnboardingVehicleCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var existingVehicles = await _vehicleRepository.GetByOwnerIdAsync(request.UserId);
        
        Vehicle existingVehicle;

        if (existingVehicles.Any())
        {
            var vehicleToUpdate = existingVehicles.FirstOrDefault()!;
            
            var plateExists = existingVehicles.Any(v => v.Id != vehicleToUpdate.Id && v.PlateNumber.Equals(request.PlateNumber, StringComparison.OrdinalIgnoreCase));
            if (plateExists)
            {
                return Result<Guid>.Failure("A vehicle with this plate number already exists.", ErrorCode.Conflict);
            }
            
            var qrPayload = $"{request.UserId}:{request.PlateNumber}:{request.Brand}";
            var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
            var qrCodeHash = HashQrBytes(qrBytes);
            
            vehicleToUpdate.Update(request.PlateNumber, request.Brand, request.VehicleType, qrCodeHash);
            await _vehicleRepository.UpdateAsync(vehicleToUpdate);
            existingVehicle = vehicleToUpdate;
        }
        else
        {
            if (existingVehicles.Count() >= 5)
            {
                return Result<Guid>.Failure("Onboarding failed. A maximum of 5 vehicles are allowed per account.", ErrorCode.Conflict);
            }

            var qrPayload = $"{request.UserId}:{request.PlateNumber}:{request.Brand}";
            var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
            var qrCodeHash = HashQrBytes(qrBytes);

            var vehicle = new Vehicle(request.UserId, request.PlateNumber, request.Brand, qrCodeHash, request.VehicleType);
            vehicle.SetPrimary(true);

            await _vehicleRepository.AddAsync(vehicle);
            existingVehicle = vehicle;
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateOnboardingStep(OnboardingStep.Schedule);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<Guid>.Success(existingVehicle.Id, "Vehicle onboarding completed.");
    }

    private static string HashQrBytes(byte[] bytes)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
