using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
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

    public CreateParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository)
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
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve VEHICLE from QR
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<CreateParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        // 2. Resolve the user's profile first, then ensure the guard exists
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.userId);

        if (userProfile == null)
            return Result<CreateParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<CreateParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        // 3. Check active parking log
        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (active != null)
            return Result<CreateParkingLogResponse>.Failure("Vehicle is already parked.", ErrorCode.Conflict);

        // // 4. Validate COR submission
        // var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        // var verifiedCor = corSubmissions.FirstOrDefault(c =>
        //     c.UserAccountId == vehicle.OwnerId &&
        //     c.VerificationStatus == CorVerificationStatus.Verified);

        // if (verifiedCor == null)
        //     return Result<CreateParkingLogResponse>.Failure("User does not have a verified COR submission.", ErrorCode.Forbidden);

        // 5. Validate schedule
        // var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);

        // var todayDayOfWeek = request.EntryTime.DayOfWeek;

        // var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

        // if (todaySchedule == null)
        //     return Result<CreateParkingLogResponse>.Failure("No parking schedule for today.", ErrorCode.Forbidden);

        // var entryTimeOfDay = request.EntryTime.TimeOfDay;

        // var earliestAllowedEntry = todaySchedule.StartTime.Add(TimeSpan.FromMinutes(-30));

        // if (entryTimeOfDay < earliestAllowedEntry || entryTimeOfDay > todaySchedule.EndTime)
        //     return Result<CreateParkingLogResponse>.Failure("Entry time does not align with parking schedule.", ErrorCode.BadRequest);

        // 6. Create parking log
        var parkingLog = new ParkingLog(vehicle.Id, guard.UserProfileId, request.EntryTime, ParkingStatus.Parked);

        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        // Build response using vehicle owner profile and related subtype
        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

        if (ownerProfile == null)
            return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        string role;
        string idNumber = string.Empty;
        string? course = null;
        int? yearLevel = null;
        string? section = null;
        string? department = null;

        if (student != null)
        {
            role = "student";
            idNumber = student.StudentNumber;
            course = student.Course;
            yearLevel = student.YearLevel;
            section = student.Section;
        }
        else if (personnel != null)
        {
            role = "personnel";
            idNumber = personnel.IdCardNumber;
            department = personnel.Department;
        }
        else if (admin != null)
        {
            role = "admin";
        }
        else if (ownerProfile.Guard != null)
        {
            role = "guard";
        }
        else
        {
            role = "unknown";
        }

        var response = new CreateParkingLogResponse
        {
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            Role = role,
            Status = parkingLog.Status.ToString(),
            IdNumber = idNumber,
            Course = course,
            YearLevel = yearLevel,
            Section = section,
            Department = department,
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            EntryTime = parkingLog.EntryTime,
            EntryDate = parkingLog.EntryTime.Date
        };

        return Result<CreateParkingLogResponse>.Success(response, "Parking log created.");
    }
}