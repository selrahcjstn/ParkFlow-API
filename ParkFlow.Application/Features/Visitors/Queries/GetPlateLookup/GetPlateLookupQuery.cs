using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup;

public record GetPlateLookupQuery(string PlateNumber, bool IdentityConfirmed = false) : IRequest<Result<UnifiedPlateLookupDto>>;

public class GetPlateLookupHandler : IRequestHandler<GetPlateLookupQuery, Result<UnifiedPlateLookupDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IVisitorRepository _visitorRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRepository _parkingLogRepository;

    public GetPlateLookupHandler(
        IVehicleRepository vehicleRepository,
        IVisitorRepository visitorRepository,
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IParkingLogRepository parkingLogRepository)
    {
        _vehicleRepository = vehicleRepository;
        _visitorRepository = visitorRepository;
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _parkingLogRepository = parkingLogRepository;
    }

    public async Task<Result<UnifiedPlateLookupDto>> Handle(GetPlateLookupQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlateNumber))
        {
            return Result<UnifiedPlateLookupDto>.Failure("Plate number is required.", ErrorCode.BadRequest);
        }

        var normalizedPlate = VisitorVehicle.NormalizePlate(request.PlateNumber);
        if (!System.Text.RegularExpressions.Regex.IsMatch(normalizedPlate, "^[A-Z0-9]{2,20}$"))
            return Result<UnifiedPlateLookupDto>.Failure("Enter a valid plate number.", ErrorCode.BadRequest);

        // 1. PRIORITY 1: Check registered ParkFlow vehicles
        var vehicle = await _vehicleRepository.GetByPlateNumberAsync(normalizedPlate);
        if (vehicle != null)
        {
            var profile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
            var ownerName = profile != null
                ? $"{profile.FirstName} {profile.LastName}".Trim()
                : "Registered User";

            string ownerRole = "Student";
            if (profile != null)
            {
                var student = await _studentRepository.GetByUserProfileIdAsync(profile.Id);
                var personnel = await _personnelRepository.GetByUserProfileIdAsync(profile.Id);
                var admin = await _adminRepository.GetByUserProfileIdAsync(profile.Id);

                if (admin != null) ownerRole = "Admin";
                else if (personnel != null) ownerRole = personnel.Role == Roles.NonAcademicPersonnel ? "University Staff" : "Faculty";
                else if (student != null) ownerRole = "Student";
            }

            var activeLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);
            var isInside = activeLog != null;

            var dto = new UnifiedPlateLookupDto(
                LookupType: "Registered",
                IsRegistered: true,
                IsVisitor: false,
                IsInside: isInside,
                PlateNumber: vehicle.PlateNumber,
                VehicleId: vehicle.Id,
                OwnerId: vehicle.OwnerId,
                OwnerName: ownerName,
                OwnerRole: ownerRole,
                Brand: vehicle.Brand,
                VehicleType: vehicle.VehicleType.ToString(),
                VehicleTypeValue: (int)vehicle.VehicleType,
                ActiveSessionId: activeLog?.Id,
                EntryTime: activeLog?.EntryTime,
                EntryMethod: activeLog?.EntryMethod.ToString(),
                ProfilePictureUrl: profile?.ProfilePictureUrl, VehiclePictureUrl: vehicle.VehiclePictureUrl
            );

            return Result<UnifiedPlateLookupDto>.Success(dto, "Registered vehicle found.");
        }

        // 2. PRIORITY 2: Check existing Visitors
        var visitorVehicle = await _visitorRepository.GetVehicleByPlateNumberAsync(normalizedPlate);
        if (visitorVehicle != null)
        {
            var visitor = visitorVehicle.Visitor;
            var activeSession = await _visitorRepository.GetActiveVisitSessionByVehicleIdAsync(visitorVehicle.Id);
            var isInside = activeSession != null;

            var dto = new UnifiedPlateLookupDto(
                LookupType: "Visitor",
                IsRegistered: false,
                IsVisitor: true,
                IsInside: isInside,
                PlateNumber: visitorVehicle.PlateNumber,
                VisitorId: request.IdentityConfirmed ? visitor.Id : null,
                FullName: request.IdentityConfirmed ? visitor.FullName : null,
                ContactNumber: request.IdentityConfirmed ? visitor.ContactNumber : null,
                Brand: visitorVehicle.Brand,
                VehicleType: visitorVehicle.VehicleType.ToString(),
                VehicleTypeValue: (int)visitorVehicle.VehicleType,
                ActiveSessionId: activeSession?.Id,
                EntryTime: activeSession?.EntryTime,
                Purpose: request.IdentityConfirmed ? activeSession?.Purpose : null,
                Destination: request.IdentityConfirmed ? activeSession?.Destination : null,
                DriversLicenseUrl: request.IdentityConfirmed ? visitor.DriversLicenseUrl : null,
                ProfilePictureUrl: request.IdentityConfirmed ? visitor.ProfilePictureUrl : null,
                VehiclePictureUrl: request.IdentityConfirmed ? visitorVehicle.VehiclePictureUrl : null,
                VisitorVehicleId: visitorVehicle.Id, IdentityConfirmed: request.IdentityConfirmed
            );

            return Result<UnifiedPlateLookupDto>.Success(dto, "Visitor record found.");
        }

        // 3. PRIORITY 3: Unknown / First-time Visitor
        return Result<UnifiedPlateLookupDto>.Success(new UnifiedPlateLookupDto(
            LookupType: "Unknown",
            IsRegistered: false,
            IsVisitor: false,
            IsInside: false,
            PlateNumber: normalizedPlate
        ), "Plate not found in system.");
    }
}
