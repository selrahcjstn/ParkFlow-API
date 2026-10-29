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
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200).WithMessage("Visitor name is required (up to 200 characters).");
        RuleFor(x => x.ContactNumber).NotEmpty().Matches(@"^09\d{9}$")
            .WithMessage("Enter an 11-digit phone number starting with 09.");
        RuleFor(x => x.LicenseVerified).Equal(true).WithMessage("Verify the driver's valid license at the gate before entry.");
        RuleFor(x => x.PlateNumber)
            .NotEmpty().WithMessage("Plate number is required.")
            .MaximumLength(20).WithMessage("Plate number must not exceed 20 characters.")
            .Must(p => System.Text.RegularExpressions.Regex.IsMatch(VisitorVehicle.NormalizePlate(p), "^[A-Z0-9]{2,20}$"))
            .WithMessage("Enter a valid plate number using letters, numbers, spaces or hyphens.");

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
        var normalizedPlate = VisitorVehicle.NormalizePlate(request.PlateNumber);

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
        var vehicle = await _visitorRepository.GetVehicleByPlateNumberAsync(normalizedPlate);
        var newVehicle = vehicle == null;
        if (request.ExistingVisitorId.HasValue && !request.IdentityConfirmed)
            return Result<VisitorEntryResponse>.Failure("Confirm the existing visitor's identity before adding a vehicle.", ErrorCode.BadRequest);
        if (vehicle != null && request.ExistingVisitorId.HasValue && request.ExistingVisitorId != vehicle.VisitorId)
            return Result<VisitorEntryResponse>.Failure("This vehicle belongs to a different visitor.", ErrorCode.Conflict);
        var visitor = vehicle?.Visitor ?? (request.ExistingVisitorId.HasValue ? await _visitorRepository.GetByIdAsync(request.ExistingVisitorId.Value) : null);
        if (request.ExistingVisitorId.HasValue && visitor == null)
            return Result<VisitorEntryResponse>.Failure("The selected visitor no longer exists. Search again.", ErrorCode.NotFound);
        var isReturning = visitor != null;
        visitor ??= new Visitor(request.FullName, request.ContactNumber, request.PlateNumber, request.VehicleType, request.Brand);
        vehicle ??= new VisitorVehicle(visitor.Id, request.PlateNumber, request.VehicleType, request.Brand) { Visitor = visitor };
        var activeSession = await _visitorRepository.GetActiveVisitSessionByVehicleIdAsync(vehicle.Id);
        if (activeSession != null)
            return Result<VisitorEntryResponse>.Failure("This vehicle is already inside campus. Process its exit before entering again.", ErrorCode.Conflict);

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
            if (request.VehiclePhoto != null) vehicle.VehiclePictureUrl = await ParkFlow.Application.Features.Profiles.DriverDocumentUpload.UploadAsync(request.VehiclePhoto, false, _cloud, cancellationToken);
        }
        catch (DocumentValidationException e) { return Result<VisitorEntryResponse>.Failure(e.Message, ErrorCode.BadRequest); }
        if (string.IsNullOrWhiteSpace(visitor.DriversLicenseUrl) || string.IsNullOrWhiteSpace(visitor.ProfilePictureUrl) || string.IsNullOrWhiteSpace(vehicle.VehiclePictureUrl))
            return Result<VisitorEntryResponse>.Failure("Capture the driver's license, driver photo and vehicle photo before entry.", ErrorCode.BadRequest);
        visitor.UpdateProfile(isReturning ? null : request.FullName, request.ContactNumber, visitor.VehicleType, null);
        visitor.LicenseVerifiedBy = request.GuardUserId;
        visitor.LicenseVerifiedAt = DateTime.UtcNow;
        if (!isReturning) visitor.VehiclePictureUrl = vehicle.VehiclePictureUrl;

        // 5. Create new Visit Session
        var newSession = new VisitSession(
            visitor.Id,
            request.Purpose,
            request.Destination,
            guardProfileId,
            entryGate);

        newSession.DriversLicenseUrl = visitor.DriversLicenseUrl;
        newSession.ProfilePictureUrl = visitor.ProfilePictureUrl;
        newSession.VehiclePictureUrl = vehicle.VehiclePictureUrl;
        newSession.VisitorVehicleId = vehicle.Id;
        newSession.Visitor = visitor;
        newSession.VisitorVehicle = vehicle;
        newSession.LicenseVerifiedBy = request.GuardUserId;
        newSession.LicenseVerifiedAt = DateTime.UtcNow;
        try { await _visitorRepository.SaveEntryAsync(visitor, vehicle, newSession, !isReturning, newVehicle, cancellationToken); }
        catch (ParkingCapacityException e) { return Result<VisitorEntryResponse>.Failure(e.Message, ErrorCode.Conflict); }

        // 6. SignalR live notification
        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("VisitorEntryRecorded", new
                {
                    visitorId = visitor.Id,
                    sessionId = newSession.Id,
                    plateNumber = vehicle.PlateNumber,
                    visitorVehicleId = vehicle.Id,
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
            PlateNumber: vehicle.PlateNumber,
            Brand: vehicle.Brand,
            VehicleType: vehicle.VehicleType.ToString(),
            EntryTime: newSession.EntryTime,
            Purpose: newSession.Purpose,
            Destination: newSession.Destination,
            Status: newSession.Status.ToString(),
            IsReturning: isReturning,
            VisitorVehicleId: vehicle.Id
        );

        return Result<VisitorEntryResponse>.Success(response, isReturning ? "Returning visitor entry recorded." : "New visitor entry recorded.");
    }
}
