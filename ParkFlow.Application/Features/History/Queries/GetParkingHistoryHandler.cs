using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.History.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
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
            var ownerProfile = log.Vehicle.Owner.UserProfile;
            if (ownerProfile is null)
                continue;

            var student = ownerProfile.Student;
            var personnel = ownerProfile.Personnel;
            var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

            var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

            var entryLocal = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
            var mustExitBy = log.EntryTime; // Fallback default

            var verifiedCor = corSubmissions.FirstOrDefault(c => 
                c.UserAccountId == log.Vehicle.OwnerId && 
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

            // Use the actual exit time from DB — null means the session is still active
            var exitTimeVal = log.ExitTime;
            var duration = exitTimeVal.HasValue
                ? (exitTimeVal.Value - log.EntryTime).TotalHours
                : (double?)null;

            var hasViolation = false;
            decimal violationFee = 0m;

            var existingViolation = await _violationRepository.GetByLogIdAsync(log.Id);
            if (existingViolation != null)
            {
                hasViolation = true;
                violationFee = existingViolation.PenaltyFee;
            }
            else if (log.ExitTime.HasValue && log.ExitTime.Value > mustExitBy)
            {
                hasViolation = true;
                var overstayDuration = log.ExitTime.Value - mustExitBy;
                violationFee = _violationService.CalculatePenalty(overstayDuration);
                if (violationFee == 0m)
                {
                    violationFee = 100.00m;
                }
            }

            dtoList.Add(new ParkingHistoryResponse
            {
                FirstName = ownerProfile.FirstName,
                LastName = ownerProfile.LastName,
                MiddleName = ownerProfile.MiddleName,
                RoleName = roleDetails.Role,
                PlateNumber = log.Vehicle.PlateNumber,
                Brand = log.Vehicle.Brand,
                Type = log.Vehicle.VehicleType.ToString(),
                EntryTime = log.EntryTime,
                ExitTime = exitTimeVal,
                ParkingDuration = duration,
                HasViolation = hasViolation,
                ViolationFee = violationFee
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
