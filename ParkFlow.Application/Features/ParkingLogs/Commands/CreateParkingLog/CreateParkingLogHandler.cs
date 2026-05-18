using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
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
    private readonly IParkingService _parkingService;
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
        IParkingService parkingService,
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
        _parkingService = parkingService;
        _scheduleService = scheduleService;
        _parkingLogRoleService = parkingLogRoleService;
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve VEHICLE from QR
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<CreateParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (activeParkingLog != null)
            return Result<CreateParkingLogResponse>.Failure("Vehicle is already parked.", ErrorCode.Conflict);

        // 2. Resolve the user's profile first, then ensure the guard exists
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (userProfile == null)
            return Result<CreateParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<CreateParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        // 3. Validate COR submission
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        var verifiedCor = corSubmissions.FirstOrDefault(c =>
            c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);

        if (verifiedCor == null)
            return Result<CreateParkingLogResponse>.Failure("User does not have a verified COR submission.", ErrorCode.Forbidden);

        // 4. Validate schedule
        var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);

        var localNow = DateTime.Now;
        var todayDayOfWeek = localNow.DayOfWeek;

        var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

        if (todaySchedule == null)
            return Result<CreateParkingLogResponse>.Failure("No parking schedule for today.", ErrorCode.Forbidden);

        var currentTime = localNow;

        if (!_scheduleService.CanEnter(currentTime, todaySchedule))
            return Result<CreateParkingLogResponse>.Failure("Entry time does not align with parking schedule.", ErrorCode.BadRequest);

        // 5. Create parking log
        var parkingLog = _parkingService.CreateEntry(vehicle.Id, guard.UserProfileId);

        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        // Calculate grace periods and estimated times (30 minutes grace period)
        var entryGracePeriod = _parkingService.CalculateEntryGracePeriod(parkingLog.EntryTime, todaySchedule.StartTime);
        var estimatedExitTime = _parkingService.CalculateEstimatedExitTime(parkingLog.EntryTime, todaySchedule.EndTime);
        
        // Retrieve the maximum exit time (which is currently acting as Unspecified/Local)
        var maximumExitTimeLocal = _parkingService.CalculateMaximumExitTime(parkingLog.EntryTime, todaySchedule.EndTime);

        // FIX: Explicitly set the kind to Local, then convert to UTC so it matches EntryTime
        var maximumExitTimeUtc = DateTime.SpecifyKind(maximumExitTimeLocal, DateTimeKind.Local).ToUniversalTime();

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
            MaximumExitTime = maximumExitTimeUtc // Use the corrected UTC time here
        };

        return Result<CreateParkingLogResponse>.Success(response, "Parking log created.");
    }
}