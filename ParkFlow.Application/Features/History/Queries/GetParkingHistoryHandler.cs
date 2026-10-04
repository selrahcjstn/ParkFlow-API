using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.History.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.History.Queries;

public class GetParkingHistoryHandler : IRequestHandler<GetParkingHistoryQuery, Result<PagedParkingHistoryResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IViolationRepository _violationRepository;
    private readonly IViolationService _violationService;

    public GetParkingHistoryHandler(
        IParkingLogRepository parkingLogRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IViolationRepository violationRepository,
        IViolationService violationService)
    {
        _parkingLogRepository = parkingLogRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _parkingLogRoleService = parkingLogRoleService;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _violationRepository = violationRepository;
        _violationService = violationService;
    }

    public async Task<Result<PagedParkingHistoryResponse>> Handle(GetParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        Guid? filterUserId = request.UserId;

        if (request.UserId != Guid.Empty)
        {
            var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
            if (profile == null)
                return Result<PagedParkingHistoryResponse>.Failure("User profile not found.", ErrorCode.NotFound);

            var guard = await _guardRepository.GetByUserProfileIdAsync(profile.Id);
            var isGuard = guard != null;

            if (isGuard)
            {
                filterUserId = null;
            }
        }
        else
        {
            filterUserId = null;
        }

        var logs = await _parkingLogRepository.GetParkingHistoryAsync(
            filterUserId,
            request.PageNumber,
            request.PageSize);

        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        var dtoList = new List<ParkingHistoryResponse>();
        foreach (var log in logs)
        {
            var vehicle = log.Vehicle;
            var ownerProfile = vehicle?.Owner?.UserProfile;
            var student = ownerProfile?.Student;
            var personnel = ownerProfile?.Personnel;
            var admin = ownerProfile != null ? await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id) : null;
            var roleDetails = ownerProfile != null
                ? _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin)
                : null;

            var entryLocal = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
            var mustExitBy = log.EntryTime; // Fallback default

            if (vehicle != null)
            {
                var verifiedCor = corSubmissions.FirstOrDefault(c => 
                    c.UserAccountId == vehicle.OwnerId && 
                    c.VerificationStatus == CorVerificationStatus.Verified);

                if (verifiedCor != null)
                {
                    var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
                    var schedule = schedules.FirstOrDefault(s => s.DayOfWeek == entryLocal.DayOfWeek);
                    if (schedule != null)
                    {
                        mustExitBy = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(entryLocal, schedule.EndTime);
                    }
                }
            }

            // Use the actual exit time from DB — null means the session is still active
            var exitTimeVal = log.ExitTime ?? log.UpdatedAt;
            var duration = exitTimeVal.HasValue
                ? Math.Round((exitTimeVal.Value - log.EntryTime).TotalHours, 2)
                : (double?)null;

            var hasViolation = false;
            decimal violationFee = 0m;
            double overstayHours = 0;
            bool isPaid = true;
            string? referenceNumber = null;

            var existingViolation = await _violationRepository.GetByLogIdAsync(log.Id);
            if (existingViolation != null)
            {
                hasViolation = true;
                violationFee = existingViolation.PenaltyFee;
                isPaid = existingViolation.SettlementStatus == SettlementStatus.Settled;
                referenceNumber = existingViolation.ReferenceNumber;
                if (exitTimeVal.HasValue && exitTimeVal.Value > mustExitBy)
                {
                    overstayHours = Math.Round((exitTimeVal.Value - mustExitBy).TotalHours, 2);
                }
            }
            else
            {
                hasViolation = false;
                isPaid = true;
                violationFee = 0m;
                overstayHours = 0;
            }

            var sessionCharge = hasViolation ? violationFee : (log.EntryMethod == EntryMethod.Manual ? 20m : 0m);

            var ownerEmail = vehicle?.Owner?.PrimaryEmail
                ?? vehicle?.Owner?.AuthIdentities?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Email))?.Email
                ?? string.Empty;

            var firstName = ownerProfile?.FirstName ?? (string.IsNullOrWhiteSpace(vehicle?.PlateNumber) ? "Registered" : "Guest");
            var lastName = ownerProfile?.LastName ?? (string.IsNullOrWhiteSpace(vehicle?.PlateNumber) ? "Driver" : "Visitor");
            var roleName = !string.IsNullOrWhiteSpace(roleDetails?.Role) ? roleDetails.Role : "Client";
            var plateNum = vehicle?.PlateNumber ?? "N/A";
            var brand = vehicle?.Brand ?? "—";
            var vehType = vehicle?.VehicleType.ToString() ?? "Car";

            dtoList.Add(new ParkingHistoryResponse
            {
                SessionId = log.Id,
                EntryMethod = log.EntryMethod.ToString(),
                FirstName = firstName,
                LastName = lastName,
                MiddleName = ownerProfile?.MiddleName,
                Email = ownerEmail,
                RoleName = roleName,
                PlateNumber = plateNum,
                Brand = brand,
                Type = vehType,
                EntryTime = log.EntryTime,
                ExitTime = exitTimeVal,
                ParkingDuration = duration,
                TotalParkingHours = duration,
                HasViolation = hasViolation,
                ViolationFee = violationFee,
                PenaltyFee = violationFee,
                Amount = sessionCharge,
                OverstayHours = overstayHours,
                IsPaid = isPaid,
                ReferenceNumber = referenceNumber,
                Status = hasViolation ? "Overdue" : "Completed"
            });
        }

        var response = new PagedParkingHistoryResponse
        {
            GeneratedAt = DateTime.UtcNow,
            Items = dtoList
        };

        return Result<PagedParkingHistoryResponse>.Success(response, "Parking history retrieved successfully.");
    }
}
