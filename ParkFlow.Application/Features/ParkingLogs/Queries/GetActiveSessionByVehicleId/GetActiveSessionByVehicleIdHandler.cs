using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;

public class GetActiveSessionByVehicleIdHandler
    : IRequestHandler<GetActiveSessionByVehicleIdQuery, Result<ActiveParkingSessionResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IViolationService _violationService;

    public GetActiveSessionByVehicleIdHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IViolationService violationService)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _violationService = violationService;
    }

    public async Task<Result<ActiveParkingSessionResponse>> Handle(
        GetActiveSessionByVehicleIdQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Fetch active parking log
        var activeLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(request.VehicleId);
        if (activeLog == null)
        {
            return Result<ActiveParkingSessionResponse>.Failure(
                "No active parking session found for this vehicle.",
                ErrorCode.NotFound);
        }

        // 2. Fetch vehicle to get owner ID
        var vehicle = await _vehicleRepository.GetByIdAsync(activeLog.VehicleId);
        if (vehicle == null)
        {
            return Result<ActiveParkingSessionResponse>.Failure(
                "Vehicle not found.",
                ErrorCode.NotFound);
        }

        // 3. Compute exitBy and accruedCharge
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var verifiedCor = corSubmissions.FirstOrDefault(c =>
            c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);

        var nowUtc = DateTime.UtcNow;
        decimal accruedCharge = 0m;
        var overstayHours = 0d;

        // scheduleDeadlineUtc = the raw schedule end time (shown to user as ExitBy)
        // maximumExitTimeUtc  = schedule end + 30 min grace (overtime starts after this)
        DateTime? scheduleDeadlineUtc = null;
        DateTime? maximumExitTimeUtc = null;
        if (verifiedCor != null)
        {
            var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
            var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(activeLog.EntryTime);
            var todaySchedule = schedules?.FirstOrDefault(s => s.DayOfWeek == philippinesEntry.DayOfWeek);

            if (todaySchedule != null)
            {
                scheduleDeadlineUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(
                    philippinesEntry,
                    todaySchedule.EndTime);

                maximumExitTimeUtc = scheduleDeadlineUtc.Value.AddMinutes(30);
            }
        }

        if (maximumExitTimeUtc.HasValue && nowUtc > maximumExitTimeUtc.Value)
        {
            var overstayDuration = nowUtc - maximumExitTimeUtc.Value;
            overstayHours = overstayDuration.TotalHours;
            accruedCharge = _violationService.CalculatePenalty(overstayDuration);
        }

        // ElapsedMinutes: always from actual DB entry time to now
        var elapsedMinutes = (int)(nowUtc - activeLog.EntryTime).TotalMinutes;

        var response = new ActiveParkingSessionResponse
        {
            SessionId = activeLog.Id.ToString(),
            StartedAt = activeLog.EntryTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ElapsedMinutes = Math.Max(0, elapsedMinutes),
            OverstayHours = overstayHours,
            AccruedCharge = accruedCharge,
            ExitBy = maximumExitTimeUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ")
                ?? scheduleDeadlineUtc?.ToString("yyyy-MM-ddTHH:mm:ssZ")
                ?? "N/A"
        };

        return Result<ActiveParkingSessionResponse>.Success(response, "Active parking session retrieved successfully.");
    }
}
