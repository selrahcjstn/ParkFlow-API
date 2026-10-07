using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount;

public class GetSessionCountHandler
    : IRequestHandler<GetSessionCountQuery, Result<SessionCountResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly ICacheService? _cacheService;

    public GetSessionCountHandler(
        IParkingLogRepository parkingLogRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IParkingReservationRepository? reservationRepository = null,
        ICacheService? cacheService = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _reservationRepository = reservationRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<SessionCountResponse>> Handle(
        GetSessionCountQuery request,
        CancellationToken cancellationToken)
    {
        var sysSettings = SystemSettingsStore.Current;
        var effectiveCapacity = request.ParkingCapacity > 0
            ? request.ParkingCapacity
            : (sysSettings.TotalCapacity > 0 ? sysSettings.TotalCapacity : 500);

        var cacheKey = CacheKeys.ActiveSessionCount(effectiveCapacity);

        async Task<SessionCountResponse> LoadAsync()
        {
            var logs = await _parkingLogRepository.GetActiveParkingLogsAsync(Math.Max(1000, effectiveCapacity));

            var nowUtc = DateTime.UtcNow;
            var graceMin = sysSettings.IsGracePeriodEnabled ? sysSettings.GracePeriodMinutes : 0;

            var activeLogs = logs
                .Where(x => x.EntryTime != default)
                .ToList();

            var overstayCount = 0;

            var ownerIds = activeLogs.Where(log => log.Vehicle != null).Select(log => log.Vehicle.OwnerId).Distinct().ToArray();
            var corSubmissions = (await _corSubmissionRepository.GetByUserIdsAsync(ownerIds)).ToList();
            var relevantCor = corSubmissions;
            var schedulesBySubmission = (await _parkingScheduleRepository.GetBySubmissionIdsAsync(relevantCor.Select(cor => cor.Id)))
                .ToLookup(schedule => schedule.SubmissionId);
            var reservationsByOwner = (_reservationRepository == null
                ? new List<ParkFlow.Domain.Entities.ParkingReservation>()
                : (await _reservationRepository.GetByUserIdsAsync(ownerIds)).ToList()).ToLookup(reservation => reservation.UserId);

            foreach (var log in activeLogs)
            {
                if (log.Vehicle == null) continue;
                var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
                DateTime? maximumExitTimeUtc = null;

                var userReservations = reservationsByOwner[log.Vehicle.OwnerId].ToList();
                var entryReservation = userReservations.FirstOrDefault(r =>
                    (r.VehicleId == log.VehicleId || r.VehicleId == null) &&
                    r.ReservationDate.Date == philippinesEntry.Date &&
                    r.Status == ReservationStatus.Approved)
                    ?? userReservations.FirstOrDefault(r =>
                        r.ReservationDate.Date == philippinesEntry.Date &&
                        r.Status == ReservationStatus.Approved);

                if (entryReservation != null)
                {
                    if (entryReservation.Type == ReservationType.Special)
                    {
                        maximumExitTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(23, 59, 59));
                    }
                    else
                    {
                        var resEndTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, entryReservation.EndTime);
                        maximumExitTimeUtc = resEndTimeUtc.AddMinutes(graceMin);
                    }
                }
                else
                {
                    var verifiedCor = corSubmissions.FirstOrDefault(c =>
                        c.UserAccountId == log.Vehicle.OwnerId &&
                        c.VerificationStatus == CorVerificationStatus.Verified);

                    if (log.EntryMethod != EntryMethod.Manual && verifiedCor != null)
                    {
                        var schedules = schedulesBySubmission[verifiedCor.Id];

                        var todaySchedule = schedules?
                            .FirstOrDefault(s =>
                                s.DayOfWeek == philippinesEntry.DayOfWeek);

                        if (todaySchedule != null)
                        {
                            var scheduleEndUtc =
                                ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(
                                    philippinesEntry,
                                    todaySchedule.EndTime);
                            maximumExitTimeUtc = scheduleEndUtc.AddMinutes(graceMin);
                        }
                    }
                }

                if (log.EntryMethod == EntryMethod.Manual)
                {
                    var entryMidnightUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(23, 59, 59));
                    maximumExitTimeUtc = entryMidnightUtc;
                }
                else if (maximumExitTimeUtc == null)
                {
                    var defaultClosingUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(22, 0, 0));
                    maximumExitTimeUtc = defaultClosingUtc > log.EntryTime ? defaultClosingUtc : log.EntryTime.AddHours(4);
                }

                if (maximumExitTimeUtc.HasValue && nowUtc > maximumExitTimeUtc.Value)
                {
                    overstayCount++;
                }
            }

            var manualSessionCount = activeLogs.Count(x => x.EntryMethod == EntryMethod.Manual || x.EntryMethod == EntryMethod.ManualScheduled);

            var response = new SessionCountResponse(
                ActiveSessionCount: activeLogs.Count,
                OverstayCount: overstayCount,
                MaximumCapacity: effectiveCapacity,
                ManualSessionCount: manualSessionCount);

            return response;
        }
        var response = _cacheService != null
            ? await _cacheService.GetOrCreateAsync(cacheKey, LoadAsync, TimeSpan.FromSeconds(20), cancellationToken)
            : await LoadAsync();

        return Result<SessionCountResponse>.Success(response, "Session count retrieved.");
    }
}
