using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Visitors.Commands.CreateVisitorEntry;

public class CreateVisitorEntryValidator : AbstractValidator<CreateVisitorEntryCommand>
{
    public CreateVisitorEntryValidator()
    {
        RuleFor(x => x.ContactNumber).NotEmpty().Matches(@"^09\d{9}$")
            .WithMessage("Enter an 11-digit phone number starting with 09.");
        RuleFor(x => x.LicenseVerified).Equal(true).WithMessage("Verify the driver's valid license at the gate before entry.");
        RuleFor(x => x.PlateNumber)
            .NotEmpty().WithMessage("Plate number is required.")
            .MaximumLength(20).WithMessage("Plate number must not exceed 20 characters.");

        RuleFor(x => x.Brand)
            .NotEmpty().WithMessage("Vehicle brand/model is required.")
            .MaximumLength(100).WithMessage("Brand must not exceed 100 characters.");

        RuleFor(x => x.VehicleType)
            .IsInEnum().WithMessage("Valid VehicleType is required.");
    }
}

public class CreateVisitorEntryHandler : IRequestHandler<CreateVisitorEntryCommand, Result<VisitorEntryResponse>>
{
    private readonly IVisitorRepository _visitorRepository;
    private readonly ICloudinaryService _cloud;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public CreateVisitorEntryHandler(
        IVisitorRepository visitorRepository,
        ICloudinaryService cloud,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IAdminRepository adminRepository,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _visitorRepository = visitorRepository;
        _cloud = cloud;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _adminRepository = adminRepository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<VisitorEntryResponse>> Handle(CreateVisitorEntryCommand request, CancellationToken cancellationToken)
    {
        var normalizedPlate = request.PlateNumber.Trim().ToUpper();

        // 1. Registered ParkFlow vehicles should use standard parking entry
        var registeredVehicle = await _vehicleRepository.GetByPlateNumberAsync(normalizedPlate);
        if (registeredVehicle != null)
        {
            return Result<VisitorEntryResponse>.Failure(
                "This plate is already registered to a BulSU account. Please use standard registered vehicle entry.",
                ErrorCode.Conflict);
        }

        var validation = await new CreateVisitorEntryValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return Result<VisitorEntryResponse>.Failure(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)), ErrorCode.BadRequest);
        if (request.VehicleType is not (VehicleType.Motorcycle or VehicleType.Car))
            return Result<VisitorEntryResponse>.Failure("Please select Motorcycle or Car.", ErrorCode.BadRequest);
        var visitor = await _visitorRepository.GetByPlateNumberAsync(normalizedPlate);
        var isReturning = visitor != null;
        visitor ??= new Visitor(request.FullName, request.ContactNumber, normalizedPlate, request.VehicleType, request.Brand);

        // 3. BACKEND DUPLICATE PROTECTION: Only ONE active visit session allowed
        var activeSession = await _visitorRepository.GetActiveVisitSessionByVisitorIdAsync(visitor.Id);
        if (activeSession != null)
        {
            return Result<VisitorEntryResponse>.Failure(
                $"Visitor with plate {normalizedPlate} is already inside campus (entered at {activeSession.EntryTime:hh:mm tt}). Please process exit before entering again.",
                ErrorCode.Conflict);
        }

        // 4. Resolve Guard / Admin
        Guid? guardProfileId = null;
        string entryGate = request.EntryGate ?? "Gate 1";

        if (request.GuardUserId.HasValue && request.GuardUserId.Value != Guid.Empty)
        {
            var userProfile = await _userProfileRepository.GetByUserIdAsync(request.GuardUserId.Value);
            if (userProfile != null)
            {
                var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
                if (guard != null)
                {
                    guardProfileId = guard.UserProfileId;
                    if (guard.AssignedGate > 0)
                    {
                        entryGate = $"Gate {guard.AssignedGate}";
                    }
                }
                else
                {
                    var admin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
                    if (admin != null)
                    {
                        // VisitSession.GuardId references Guards, not Admins.
                        guardProfileId = null;
                    }
                }
            }
        }

        if (!request.GuardUserId.HasValue || guardProfileId == null)
            return Result<VisitorEntryResponse>.Failure("Only an authorized guard can verify visitor licenses at entry.", ErrorCode.Forbidden);
        try
        {
            if (request.License != null) visitor.DriversLicenseUrl = await ParkFlow.Application.Features.Profiles.DriverDocumentUpload.UploadAsync(request.License, true, _cloud, cancellationToken);
            if (request.ProfilePhoto != null) visitor.ProfilePictureUrl = await ParkFlow.Application.Features.Profiles.DriverDocumentUpload.UploadAsync(request.ProfilePhoto, false, _cloud, cancellationToken);
            if (request.VehiclePhoto != null) visitor.VehiclePictureUrl = await ParkFlow.Application.Features.Profiles.DriverDocumentUpload.UploadAsync(request.VehiclePhoto, false, _cloud, cancellationToken);
        }
        catch (DocumentValidationException e) { return Result<VisitorEntryResponse>.Failure(e.Message, ErrorCode.BadRequest); }
        if (string.IsNullOrWhiteSpace(visitor.DriversLicenseUrl) || string.IsNullOrWhiteSpace(visitor.ProfilePictureUrl) || string.IsNullOrWhiteSpace(visitor.VehiclePictureUrl))
            return Result<VisitorEntryResponse>.Failure("Capture the driver's license, driver photo and vehicle photo before entry.", ErrorCode.BadRequest);
        visitor.UpdateProfile(isReturning ? null : request.FullName, request.ContactNumber, visitor.VehicleType, null);
        if (isReturning) await _visitorRepository.UpdateAsync(visitor);
        else await _visitorRepository.AddAsync(visitor);

        // 5. Create new Visit Session
        var newSession = new VisitSession(
            visitor.Id,
            request.Purpose,
            request.Destination,
            guardProfileId,
            entryGate);

        newSession.DriversLicenseUrl = visitor.DriversLicenseUrl;
        newSession.ProfilePictureUrl = visitor.ProfilePictureUrl;
        newSession.VehiclePictureUrl = visitor.VehiclePictureUrl;
        newSession.LicenseVerifiedBy = request.GuardUserId;
        newSession.LicenseVerifiedAt = DateTime.UtcNow;
        await _visitorRepository.AddVisitSessionAsync(newSession);

        // 6. SignalR live notification
        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("VisitorEntryRecorded", new
                {
                    visitorId = visitor.Id,
                    sessionId = newSession.Id,
                    plateNumber = visitor.PlateNumber,
                    fullName = visitor.FullName,
                    entryTime = newSession.EntryTime
                });
                await _signalRNotificationSender.SendToAllAsync("ParkingSessionUpdated", new { });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        var response = new VisitorEntryResponse(
            VisitorId: visitor.Id,
            SessionId: newSession.Id,
            FullName: visitor.FullName,
            PlateNumber: visitor.PlateNumber,
            Brand: visitor.Brand,
            VehicleType: visitor.VehicleType.ToString(),
            EntryTime: newSession.EntryTime,
            Purpose: newSession.Purpose,
            Destination: newSession.Destination,
            Status: newSession.Status.ToString(),
            IsReturning: isReturning,
            EntryFee: SystemSettingsStore.Current.ForVehicle(visitor.VehicleType).EntryFee
        );

        return Result<VisitorEntryResponse>.Success(response, isReturning ? "Returning visitor entry recorded." : "New visitor entry recorded.");
    }
}
