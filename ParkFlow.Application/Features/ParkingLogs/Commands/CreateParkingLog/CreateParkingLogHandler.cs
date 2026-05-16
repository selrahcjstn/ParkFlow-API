using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogHandler : IRequestHandler<CreateParkingLogCommand, Result<Guid>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;

    public CreateParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
    }

    public async Task<Result<Guid>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        // 1. Resolve VEHICLE from QR
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null)
            return Result<Guid>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        // 2. Resolve the user's profile first, then ensure the guard exists
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.userId);

        if (userProfile == null)
            return Result<Guid>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<Guid>.Failure("Guard not found.", ErrorCode.NotFound);

        // 3. Check active parking log
        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (active != null)
            return Result<Guid>.Failure("Vehicle is already parked.", ErrorCode.Conflict);

        // 4. Validate COR submission
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();

        var verifiedCor = corSubmissions.FirstOrDefault(c =>
            c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);

        if (verifiedCor == null)
            return Result<Guid>.Failure("User does not have a verified COR submission.", ErrorCode.Forbidden);

        // 5. Validate schedule
        var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);

        var todayDayOfWeek = request.EntryTime.DayOfWeek;

        var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

        if (todaySchedule == null)
            return Result<Guid>.Failure("No parking schedule for today.", ErrorCode.Forbidden);

        var entryTimeOfDay = request.EntryTime.TimeOfDay;

        var earliestAllowedEntry = todaySchedule.StartTime.Add(TimeSpan.FromMinutes(-30));

        if (entryTimeOfDay < earliestAllowedEntry || entryTimeOfDay > todaySchedule.EndTime)
            return Result<Guid>.Failure("Entry time does not align with parking schedule.", ErrorCode.BadRequest);

        // 6. Create parking log
        var parkingLog = new ParkingLog(vehicle.Id, guard.UserProfileId, request.EntryTime, ParkingStatus.Parked);

        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        return Result<Guid>.Success(parkingLog.Id, "Parking log created.");
    }
}