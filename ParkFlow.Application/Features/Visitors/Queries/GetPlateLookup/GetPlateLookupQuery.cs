using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup;

public record GetPlateLookupQuery(string PlateNumber) : IRequest<Result<UnifiedPlateLookupDto>>;

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

        var normalizedPlate = request.PlateNumber.Trim().ToUpper();

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
                else if (personnel != null) ownerRole = "Personnel";
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
                EntryMethod: activeLog?.EntryMethod.ToString()
            );

            return Result<UnifiedPlateLookupDto>.Success(dto, "Registered vehicle found.");
        }

        // 2. PRIORITY 2: Check existing Visitors
        var visitor = await _visitorRepository.GetByPlateNumberAsync(normalizedPlate);
        if (visitor != null)
        {
            var activeSession = await _visitorRepository.GetActiveVisitSessionByVisitorIdAsync(visitor.Id);
            var isInside = activeSession != null;

            var dto = new UnifiedPlateLookupDto(
                LookupType: "Visitor",
                IsRegistered: false,
                IsVisitor: true,
                IsInside: isInside,
                PlateNumber: visitor.PlateNumber,
                VisitorId: visitor.Id,
                FullName: visitor.FullName,
                ContactNumber: visitor.ContactNumber,
                Brand: visitor.Brand,
                VehicleType: visitor.VehicleType.ToString(),
                VehicleTypeValue: (int)visitor.VehicleType,
                ActiveSessionId: activeSession?.Id,
                EntryTime: activeSession?.EntryTime,
                Purpose: activeSession?.Purpose,
                Destination: activeSession?.Destination
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
