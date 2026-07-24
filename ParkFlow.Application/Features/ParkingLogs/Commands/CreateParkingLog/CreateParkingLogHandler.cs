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
    private readonly IViolationRepository _violationRepository;
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
        IViolationRepository violationRepository,
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
        _violationRepository = violationRepository;
        _parkingService = parkingService;
        _scheduleService = scheduleService;
        _parkingLogRoleService = parkingLogRoleService;
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<CreateParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        var hasActiveViolation = await _violationRepository.HasActiveViolationAsync(vehicle.Id);
        if (hasActiveViolation)
            return Result<CreateParkingLogResponse>.Failure("Vehicle has active/unpaid violations. Entry denied.", ErrorCode.Forbidden);

        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

        if (ownerProfile == null)
            return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (activeParkingLog != null)
        {
            var conflictResponse = new CreateParkingLogResponse
            {
                FirstName = ownerProfile.FirstName,
                LastName = ownerProfile.LastName,
                MiddleName = ownerProfile.MiddleName,
                PlateNumber = vehicle.PlateNumber,
                Brand = vehicle.Brand,
                QrCodeHash = vehicle.QrCodeHash,
                VehicleType = vehicle.VehicleType.ToString(),
                EntryMethod = activeParkingLog.EntryMethod.ToString()
            };

            return Result<CreateParkingLogResponse>.Failure(
                conflictResponse,
                "Vehicle is already parked.",
                ErrorCode.Conflict);
        }

        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (userProfile == null)
            return Result<CreateParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<CreateParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var isStudentOrPersonnel = (student != null || personnel != null) && admin == null;

        DateTime? maximumExitTimeUtc = null;

        if (isStudentOrPersonnel)
        {
            var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

            var verifiedCor = corSubmissions.FirstOrDefault(c =>
                c.UserAccountId == vehicle.OwnerId &&
                c.VerificationStatus == CorVerificationStatus.Verified);

            if (verifiedCor == null)
            {
                var docName = student != null ? "Student COR" : "Personnel ID / Registration";
                return Result<CreateParkingLogResponse>.Failure(
                    $"Entry denied: {docName} document is missing or unverified.",
                    ErrorCode.Forbidden);
            }

            var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);

            var utcNow = DateTime.UtcNow;
            var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(utcNow);
            var todayDayOfWeek = philippinesNow.DayOfWeek;

            var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

            if (todaySchedule == null)
            {
                return Result<CreateParkingLogResponse>.Failure(
                    "Entry denied: No class or work schedule submitted for today.",
                    ErrorCode.Forbidden);
            }

            if (!_scheduleService.CanEnter(philippinesNow, todaySchedule))
            {
                return Result<CreateParkingLogResponse>.Failure(
                    "Entry denied: Entry time does not align with authorized schedule.",
                    ErrorCode.BadRequest);
            }

            var scheduleEndTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, todaySchedule.EndTime);
            maximumExitTimeUtc = scheduleEndTimeUtc.AddMinutes(30);
        }

        var parkingLog = _parkingService.CreateEntry(vehicle.Id, guard.UserProfileId);
        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var response = new CreateParkingLogResponse
        {
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            MiddleName = ownerProfile.MiddleName,
            Role = roleDetails.Role,
            Status = parkingLog.Status.ToString(),
            PhoneNumber = ownerProfile.UserAccount?.PhoneNumber ?? string.Empty,
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            QrCodeHash = vehicle.QrCodeHash,
            VehicleType = vehicle.VehicleType.ToString(),
            EntryTime = parkingLog.EntryTime,
            EntryDate = parkingLog.EntryTime.Date,
            MaximumExitTime = maximumExitTimeUtc,
            EntryMethod = parkingLog.EntryMethod.ToString()
        };

        return Result<CreateParkingLogResponse>.Success(response, "Entry Confirmed");
    }
}
