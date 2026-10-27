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
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly IUserProfileRepository? _userProfileRepository;
    private readonly IPersonnelRepository? _personnelRepository;

    public GetActiveSessionByVehicleIdHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IViolationService violationService,
        IParkingReservationRepository? reservationRepository = null,
        IUserProfileRepository? userProfileRepository = null,
        IPersonnelRepository? personnelRepository = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _violationService = violationService;
        _reservationRepository = reservationRepository;
        _userProfileRepository = userProfileRepository;
        _personnelRepository = personnelRepository;
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

        // 3. Compute exitBy and accruedCharge
        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var verifiedCor = corSubmissions.FirstOrDefault(c =>
            c.UserAccountId == vehicle.OwnerId &&
            c.VerificationStatus == CorVerificationStatus.Verified);

        var ownerProfile = _userProfileRepository == null ? null : await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        var personnel = ownerProfile == null || _personnelRepository == null ? null : await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var isPersonnelParking = PersonnelParkingPolicy.AppliesTo(personnel ?? vehicle.Owner?.UserProfile?.Personnel);
        var nowUtc = DateTime.UtcNow;
        decimal accruedCharge = 0m;
        var overstayHours = 0d;

        // scheduleDeadlineUtc = the raw schedule / reservation end time (shown to user as ExitBy)
        // maximumExitTimeUtc  = schedule end + grace period (overtime penalty starts after this)
        DateTime? scheduleDeadlineUtc = null;
        DateTime? maximumExitTimeUtc = null;
        var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(activeLog.EntryTime);
        var sysSettings = SystemSettingsStore.Current;
        var graceMin = sysSettings.IsGracePeriodEnabled ? sysSettings.GracePeriodMinutes : 0;

        var userReservations = _reservationRepository != null 
            ? (await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId)).ToList() 
            : new List<ParkFlow.Domain.Entities.ParkingReservation>();
        bool IsReservationDateMatch(ParkFlow.Domain.Entities.ParkingReservation res)
        {
            return ReservationSessionWindow.CoversEntry(res, activeLog.EntryTime, sysSettings);
        }

        var entryReservation = userReservations.FirstOrDefault(r =>
            (r.VehicleId == vehicle.Id || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
            IsReservationDateMatch(r) &&
            (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Completed))
            ?? userReservations.FirstOrDefault(r =>
                IsReservationDateMatch(r) &&
                (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Completed))
            ?? userReservations.FirstOrDefault(r =>
                (r.VehicleId == vehicle.Id || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
                IsReservationDateMatch(r) &&
                r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Rejected)
            ?? userReservations.FirstOrDefault(r =>
                IsReservationDateMatch(r) &&
                r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Rejected);

        if (PersonnelParkingPolicy.IsFaculty(personnel ?? vehicle.Owner?.UserProfile?.Personnel)) { overstayHours = 0; scheduleDeadlineUtc = null; }
        else if (isPersonnelParking)
        {
            scheduleDeadlineUtc = PersonnelParkingPolicy.GetDeadlineUtc(activeLog.EntryTime, sysSettings);
            maximumExitTimeUtc = scheduleDeadlineUtc;
        }
        else if (entryReservation != null)
        {
            overstayHours = entryReservation.Type == ReservationType.Special || !maximumExitTimeUtc.HasValue
                ? 0 : Math.Max(0, (nowUtc - maximumExitTimeUtc.Value).TotalHours);
        }
        else
        {
            if (maximumExitTimeUtc == null)
            {
                var defaultClosingUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(22, 0, 0));
                maximumExitTimeUtc = defaultClosingUtc > activeLog.EntryTime ? defaultClosingUtc : activeLog.EntryTime.AddHours(4);
                scheduleDeadlineUtc = maximumExitTimeUtc;
            }

            if (maximumExitTimeUtc.HasValue && nowUtc > maximumExitTimeUtc.Value)
            {
                var overstayDuration = nowUtc - maximumExitTimeUtc.Value;
                overstayHours = overstayDuration.TotalHours;

            }
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
