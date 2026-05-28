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
        var existingVehicle = existingVehicles.FirstOrDefault(v => v.PlateNumber == request.PlateNumber);

        if (existingVehicle == null)
        {
            var qrPayload = $"{request.UserId}:{request.PlateNumber}:{request.Brand}";
            var qrBytes = _qrCodeService.GenerateQrCode(qrPayload);
            var qrCodeHash = HashQrBytes(qrBytes);

            var vehicle = new Vehicle(request.UserId, request.PlateNumber, request.Brand, qrCodeHash, request.VehicleType);
            await _vehicleRepository.AddAsync(vehicle);
            existingVehicle = vehicle;
        }
        else
        {
            return Result<Guid>.Failure("Vehicle with this plate number already exists.", ErrorCode.Conflict);
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
