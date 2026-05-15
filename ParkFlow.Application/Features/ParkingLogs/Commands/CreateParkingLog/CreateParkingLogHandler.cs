using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogHandler : IRequestHandler<CreateParkingLogCommand, Result<Guid>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;

    public CreateParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
    }

    public async Task<Result<Guid>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        // Ensure vehicle exists
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId);
        if (vehicle == null)
            return Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound);

        // Ensure guard exists
        var guard = await _guardRepository.GetByUserProfileIdAsync(request.GuardId);
        if (guard == null)
            return Result<Guid>.Failure("Guard not found.", ErrorCode.NotFound);

        // Check if vehicle already has active parking log
        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(request.VehicleId);
        if (active != null)
            return Result<Guid>.Failure("Vehicle is already parked.", ErrorCode.Conflict);

        // Validate COR submission
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var verifiedCor = corSubmissions.FirstOrDefault(c =>
            c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);

        if (verifiedCor == null)
            return Result<Guid>.Failure("User does not have a verified COR submission.", ErrorCode.Forbidden);

        // Validate schedule
        var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
        var todayDayOfWeek = request.EntryTime.DayOfWeek;
        var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

        if (todaySchedule == null)
            return Result<Guid>.Failure("No parking schedule for today.", ErrorCode.Forbidden);

        var entryTimeOfDay = request.EntryTime.TimeOfDay;
        var earliestAllowedEntry = todaySchedule.StartTime.Add(TimeSpan.FromMinutes(-30));

        if (entryTimeOfDay < earliestAllowedEntry || entryTimeOfDay > todaySchedule.EndTime)
            return Result<Guid>.Failure("Entry time does not align with parking schedule.", ErrorCode.BadRequest);

        // Create parking log ONLY
        var parkingLog = new ParkingLog(vehicle, guard, request.EntryTime, ParkingStatus.Parked);

        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        return Result<Guid>.Success(parkingLog.Id, "Parking log created.");
    }
}