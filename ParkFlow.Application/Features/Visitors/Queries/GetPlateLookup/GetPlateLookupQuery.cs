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
    private readonly IUserAccountRepository? _userAccountRepository;

    public GetPlateLookupHandler(
        IVehicleRepository vehicleRepository,
        IVisitorRepository visitorRepository,
        IUserProfileRepository userProfileRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IParkingLogRepository parkingLogRepository,
        IUserAccountRepository? userAccountRepository = null)
    {
        _vehicleRepository = vehicleRepository;
        _visitorRepository = visitorRepository;
        _userProfileRepository = userProfileRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _parkingLogRepository = parkingLogRepository;
        _userAccountRepository = userAccountRepository;
    }

    public async Task<Result<UnifiedPlateLookupDto>> Handle(GetPlateLookupQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.PlateNumber))
        {
            return Result<UnifiedPlateLookupDto>.Failure("Plate number or search term is required.", ErrorCode.BadRequest);
        }

        var rawQuery = request.PlateNumber.Trim();
        var normalizedPlate = VisitorVehicle.NormalizePlate(rawQuery);

        // 1. PRIORITY 1: Check registered ParkFlow vehicles by normalized plate
        if (!string.IsNullOrWhiteSpace(normalizedPlate))
        {
            var vehicle = await _vehicleRepository.GetByPlateNumberAsync(normalizedPlate);
            if (vehicle != null)
            {
                return await BuildRegisteredDtoAsync(vehicle);
            }
        }

        // 2. PRIORITY 2: Check existing Visitors by normalized plate
        if (!string.IsNullOrWhiteSpace(normalizedPlate))
        {
            var visitorVehicle = await _visitorRepository.GetVehicleByPlateNumberAsync(normalizedPlate);
            if (visitorVehicle != null)
            {
                return await BuildVisitorDtoAsync(visitorVehicle, request.IdentityConfirmed);
            }
        }

        // 3. PRIORITY 3: Search by Student ID / Number
        var student = await _studentRepository.GetByStudentNumberAsync(rawQuery);
        if (student == null && rawQuery.Length >= 4)
        {
            student = await _studentRepository.GetByStudentNumberAsync(Student.NormalizeNumber(rawQuery));
        }
        if (student != null)
        {
            var profile = student.UserProfile ?? await _userProfileRepository.GetByIdAsync(student.UserProfileId);
            if (profile != null)
            {
                var vehicles = (await _vehicleRepository.GetByOwnerIdAsync(profile.UserAccountId)).ToList();
                var matched = vehicles.FirstOrDefault(v => v.VerificationStatus == CorVerificationStatus.Verified) ?? vehicles.FirstOrDefault();
                if (matched != null)
                {
                    return await BuildRegisteredDtoAsync(matched, profile);
                }
            }
        }

        // 4. PRIORITY 4: Search by Employee ID / IdCardNumber
        var personnel = await _personnelRepository.GetByIdCardNumberAsync(rawQuery);
        if (personnel == null)
        {
            personnel = await _personnelRepository.GetByIdCardNumberAsync(Personnel.NormalizeId(rawQuery));
        }
        if (personnel != null)
        {
            var profile = personnel.UserProfile ?? await _userProfileRepository.GetByIdAsync(personnel.UserProfileId);
            if (profile != null)
            {
                var vehicles = (await _vehicleRepository.GetByOwnerIdAsync(profile.UserAccountId)).ToList();
                var matched = vehicles.FirstOrDefault(v => v.VerificationStatus == CorVerificationStatus.Verified) ?? vehicles.FirstOrDefault();
                if (matched != null)
                {
                    return await BuildRegisteredDtoAsync(matched, profile);
                }
            }
        }

        // 5. PRIORITY 5: Search by Phone Number
        if (_userAccountRepository != null)
        {
            var phoneDigits = System.Text.RegularExpressions.Regex.Replace(rawQuery, @"\D", "");
            if (phoneDigits.StartsWith("639") && phoneDigits.Length == 12) phoneDigits = "0" + phoneDigits[2..];
            if (phoneDigits.Length >= 10)
            {
                var account = await _userAccountRepository.GetByPhoneNumberAsync(phoneDigits);
                if (account != null)
                {
                    var profile = await _userProfileRepository.GetByUserIdAsync(account.Id);
                    if (profile != null)
                    {
                        var vehicles = (await _vehicleRepository.GetByOwnerIdAsync(account.Id)).ToList();
                        var matched = vehicles.FirstOrDefault(v => v.VerificationStatus == CorVerificationStatus.Verified) ?? vehicles.FirstOrDefault();
                        if (matched != null)
                        {
                            return await BuildRegisteredDtoAsync(matched, profile);
                        }
                    }
                }
            }
        }

        // 6. PRIORITY 6: Search by Name (registered users or visitors)
        if (rawQuery.Length >= 2)
        {
            var matchedProfiles = await _userProfileRepository.SearchByNameOrPhoneAsync(rawQuery, 3);
            foreach (var prof in matchedProfiles)
            {
                var vehicles = (await _vehicleRepository.GetByOwnerIdAsync(prof.UserAccountId)).ToList();
                var matched = vehicles.FirstOrDefault(v => v.VerificationStatus == CorVerificationStatus.Verified) ?? vehicles.FirstOrDefault();
                if (matched != null)
                {
                    return await BuildRegisteredDtoAsync(matched, prof);
                }
            }

            // Also search existing visitors by name or contact number
            try
            {
                var (visitorResults, _) = await _visitorRepository.GetPagedVisitorsAsync(1, 1, rawQuery);
                var firstVisitor = visitorResults?.FirstOrDefault();
                if (firstVisitor != null)
                {
                    var fullVisitor = await _visitorRepository.GetWithSessionsByIdAsync(firstVisitor.Id);
                    var matchedVisitorVehicle = fullVisitor?.Vehicles.FirstOrDefault();
                    if (matchedVisitorVehicle != null)
                    {
                        return await BuildVisitorDtoAsync(matchedVisitorVehicle, request.IdentityConfirmed);
                    }
                }
            }
            catch (NotImplementedException) { }
        }

        // 7. PRIORITY 7: Unknown / First-time Visitor
        return Result<UnifiedPlateLookupDto>.Success(new UnifiedPlateLookupDto(
            LookupType: "Unknown",
            IsRegistered: false,
            IsVisitor: false,
            IsInside: false,
            PlateNumber: string.IsNullOrWhiteSpace(normalizedPlate) ? rawQuery : normalizedPlate
        ), "Plate not found in system.");
    }

    private async Task<Result<UnifiedPlateLookupDto>> BuildRegisteredDtoAsync(Vehicle vehicle, UserProfile? profile = null)
    {
        profile ??= await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
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
            PlateNumber: !string.IsNullOrWhiteSpace(vehicle.PlateNumber) ? vehicle.PlateNumber : (vehicle.VehicleType == VehicleType.ElectricBike ? "EBIKE" : ""),
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
            ProfilePictureUrl: profile?.ProfilePictureUrl,
            VehiclePictureUrl: vehicle.VehiclePictureUrl
        );

        return Result<UnifiedPlateLookupDto>.Success(dto, "Registered vehicle found.");
    }

    private async Task<Result<UnifiedPlateLookupDto>> BuildVisitorDtoAsync(VisitorVehicle visitorVehicle, bool identityConfirmed)
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
            VisitorId: identityConfirmed ? visitor.Id : null,
            FullName: identityConfirmed ? visitor.FullName : null,
            ContactNumber: identityConfirmed ? visitor.ContactNumber : null,
            Brand: visitorVehicle.Brand,
            VehicleType: visitorVehicle.VehicleType.ToString(),
            VehicleTypeValue: (int)visitorVehicle.VehicleType,
            ActiveSessionId: activeSession?.Id,
            EntryTime: activeSession?.EntryTime,
            Purpose: identityConfirmed ? activeSession?.Purpose : null,
            Destination: identityConfirmed ? activeSession?.Destination : null,
            DriversLicenseUrl: identityConfirmed ? visitor.DriversLicenseUrl : null,
            ProfilePictureUrl: identityConfirmed ? visitor.ProfilePictureUrl : null,
            VehiclePictureUrl: identityConfirmed ? visitorVehicle.VehiclePictureUrl : null,
            VisitorVehicleId: visitorVehicle.Id,
            IdentityConfirmed: identityConfirmed
        );

        return Result<UnifiedPlateLookupDto>.Success(dto, "Visitor record found.");
    }
}
