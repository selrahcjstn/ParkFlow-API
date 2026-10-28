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
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly IUserProfileRepository? _userProfileRepository;
    private readonly IPersonnelRepository? _personnelRepository;
    private readonly TimeProvider _timeProvider;

    public GetActiveSessionByVehicleIdHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingReservationRepository? reservationRepository = null,
        IUserProfileRepository? userProfileRepository = null,
        IPersonnelRepository? personnelRepository = null,
        TimeProvider? timeProvider = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _reservationRepository = reservationRepository;
        _userProfileRepository = userProfileRepository;
        _personnelRepository = personnelRepository;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Result<ActiveParkingSessionResponse>> Handle(
        GetActiveSessionByVehicleIdQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Fetch active parking log with transient error resilience
        ParkFlow.Domain.Entities.ParkingLog? activeLog;
        try
        {
            activeLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(request.VehicleId);
        }
        catch (Exception)
        {
            return Result<ActiveParkingSessionResponse>.Failure(
                "No active parking session found for this vehicle.",
                ErrorCode.NotFound);
        }

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

        // Use the same operational policy as session lists/counts; do not infer a deadline from fees.
        var corSubmissions = (await _corSubmissionRepository.ListCorSubmissionsAsync())
            .Where(c => c.UserAccountId == vehicle.OwnerId).ToList();
        var schedules = await _parkingScheduleRepository.GetBySubmissionIdsAsync(corSubmissions.Select(c => c.Id));
        var ownerProfile = _userProfileRepository == null ? null : await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        var personnel = ownerProfile == null || _personnelRepository == null ? null : await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var reservations = _reservationRepository == null
            ? new List<ParkFlow.Domain.Entities.ParkingReservation>()
            : (await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId)).ToList();
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var timing = ActiveSessionTiming.ResolveActive(activeLog, nowUtc, SystemSettingsStore.Current,
            corSubmissions, schedules.ToLookup(s => s.SubmissionId), reservations,
            personnel ?? vehicle.Owner?.UserProfile?.Personnel, vehicle);

        // ElapsedMinutes: always from actual DB entry time to now
        var elapsedMinutes = (int)(nowUtc - activeLog.EntryTime).TotalMinutes;

        var response = new ActiveParkingSessionResponse
        {
            SessionId = activeLog.Id.ToString(),
            StartedAt = activeLog.EntryTime.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ElapsedMinutes = Math.Max(0, elapsedMinutes),
            OverstayHours = timing.OverstayHours,

            ExitBy = timing.MaximumExitTime?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? "N/A"
        };

        return Result<ActiveParkingSessionResponse>.Success(response, "Active parking session retrieved successfully.");
    }
}
