using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogHandler : IRequestHandler<CreateParkingLogCommand, Result<CreateParkingLogResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IViolationRepository _violationRepository;
    private readonly IParkingService _parkingService;
    private readonly IViolationService _violationService;
    private readonly IScheduleService _scheduleService;
    private readonly IParkingLogRoleService _parkingLogRoleService;

    public CreateParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IViolationRepository violationRepository,
        IParkingService parkingService,
        IViolationService violationService,
        IScheduleService scheduleService,
        IParkingLogRoleService parkingLogRoleService)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _violationRepository = violationRepository;
        _parkingService = parkingService;
        _violationService = violationService;
        _scheduleService = scheduleService;
        _parkingLogRoleService = parkingLogRoleService;
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve VEHICLE from QR
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<CreateParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        // 2. Resolve the user's profile first, then ensure the guard exists
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (userProfile == null)
            return Result<CreateParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<CreateParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        // 3. Check active parking log — if exists, treat as exit
        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (active != null)
        {
            var exitTime = DateTime.UtcNow;
            _parkingService.MarkExit(active);
            await _parkingLogRepository.UpdateParkingLogAsync(active);

            var corSubmissionsForExit = await _corSubmissionRepository.ListCorSubmissionsAsync();
            var verifiedCorForExit = corSubmissionsForExit.FirstOrDefault(c => c.UserAccountId == vehicle.OwnerId && c.VerificationStatus == CorVerificationStatus.Verified);

            DateTime? entryGracePeriodCalc = null;
            DateTime? exitGracePeriodCalc = null;
            decimal penaltyFeeCalc = 0m;

            if (verifiedCorForExit != null)
            {
                var schedulesForExit = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCorForExit.Id);
                var todayScheduleForExit = schedulesForExit.FirstOrDefault(s => s.DayOfWeek == exitTime.DayOfWeek);
                if (todayScheduleForExit != null)
                {
                    entryGracePeriodCalc = _parkingService.CalculateEntryGracePeriod(active.EntryTime, todayScheduleForExit.StartTime);
                    exitGracePeriodCalc = _parkingService.CalculateMaximumExitTime(active.EntryTime, todayScheduleForExit.EndTime);

                    if (_violationService.IsOverstay(exitTime, todayScheduleForExit.EndTime))
                    {
                        var overstayDuration = _violationService.GetOverstayDuration(exitTime, todayScheduleForExit.EndTime);
                        penaltyFeeCalc = _violationService.CalculatePenalty(overstayDuration);

                        if (penaltyFeeCalc > 0m)
                        {
                            var violation = new Violation(active.Id, ViolationType.Overstay, penaltyFeeCalc);
                            await _violationRepository.AddAsync(violation);
                        }
                    }
                }
            }

            var actualExitTime = active.ExitTime ?? exitTime;
            var totalParkingHours = _parkingService.CalculateTotalParkingHours(active.EntryTime, actualExitTime);

            // Build response for exit
            var ownerProfileExit = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

            if (ownerProfileExit == null)
                return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

            var studentExit = await _studentRepository.GetByUserProfileIdAsync(ownerProfileExit.Id);
            var personnelExit = await _personnelRepository.GetByUserProfileIdAsync(ownerProfileExit.Id);
            var adminExit = await _adminRepository.GetByUserProfileIdAsync(ownerProfileExit.Id);

            var roleExitDetails = _parkingLogRoleService.GetRoleDetails(ownerProfileExit, studentExit, personnelExit, adminExit);

            var exitResponse = new CreateParkingLogResponse
            {
                FirstName = ownerProfileExit.FirstName,
                LastName = ownerProfileExit.LastName,
                Role = roleExitDetails.Role,
                Status = active.Status.ToString(),
                IdNumber = roleExitDetails.IdNumber,
                Course = roleExitDetails.Course,
                YearLevel = roleExitDetails.YearLevel,
                Section = roleExitDetails.Section,
                Department = roleExitDetails.Department,
                PlateNumber = vehicle.PlateNumber,
                Brand = vehicle.Brand,
                EntryTime = active.EntryTime,
                EntryDate = active.EntryTime.Date,
                ExitTime = actualExitTime,
                EntryGracePeriod = entryGracePeriodCalc,
                ExitGracePeriod = exitGracePeriodCalc,
                MaximumExitTime = exitGracePeriodCalc,
                PenaltyFee = penaltyFeeCalc,
                TotalParkingHours = totalParkingHours
            };

            return Result<CreateParkingLogResponse>.Success(exitResponse, "Parking exit processed.");
        }

        // 4. Validate COR submission
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        var verifiedCor = corSubmissions.FirstOrDefault(c =>
            c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);

        if (verifiedCor == null)
            return Result<CreateParkingLogResponse>.Failure("User does not have a verified COR submission.", ErrorCode.Forbidden);

        // 5. Validate schedule
        var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);

        var todayDayOfWeek = DateTime.UtcNow.DayOfWeek;

        var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

        if (todaySchedule == null)
            return Result<CreateParkingLogResponse>.Failure("No parking schedule for today.", ErrorCode.Forbidden);

        var currentTime = DateTime.UtcNow;

        if (!_scheduleService.CanEnter(currentTime, todaySchedule))
            return Result<CreateParkingLogResponse>.Failure("Entry time does not align with parking schedule.", ErrorCode.BadRequest);

        // 6. Create parking log
        var parkingLog = _parkingService.CreateEntry(vehicle.Id, guard.UserProfileId);

        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        // Calculate grace periods and estimated times (30 minutes grace period)
        var entryGracePeriod = _parkingService.CalculateEntryGracePeriod(parkingLog.EntryTime, todaySchedule.StartTime);
        var estimatedExitTime = _parkingService.CalculateEstimatedExitTime(parkingLog.EntryTime, todaySchedule.EndTime);
        var maximumExitTime = _parkingService.CalculateMaximumExitTime(parkingLog.EntryTime, todaySchedule.EndTime);

        // Build response using vehicle owner profile and related subtype
        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

        if (ownerProfile == null)
            return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var response = new CreateParkingLogResponse
        {
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            Role = roleDetails.Role,
            Status = parkingLog.Status.ToString(),
            IdNumber = roleDetails.IdNumber,
            Course = roleDetails.Course,
            YearLevel = roleDetails.YearLevel,
            Section = roleDetails.Section,
            Department = roleDetails.Department,
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            EntryTime = parkingLog.EntryTime,
            EntryDate = parkingLog.EntryTime.Date,
            EntryGracePeriod = entryGracePeriod,
            EstimatedExitTime = estimatedExitTime,
            MaximumExitTime = maximumExitTime
        };

        return Result<CreateParkingLogResponse>.Success(response, "Parking log created.");
    }
}
