// Handler
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;

public class GetActiveParkingSessionHandler
    : IRequestHandler<
        GetActiveParkingSessionQuery,
        Result<IEnumerable<GetActiveParkingSessionResponse>>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IViolationService _violationService;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly ICacheService? _cacheService;

    public GetActiveParkingSessionHandler(
        IParkingLogRepository parkingLogRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IViolationService violationService,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService,
        IParkingReservationRepository? reservationRepository = null,
        ICacheService? cacheService = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _violationService = violationService;
        _adminRepository = adminRepository;
        _parkingLogRoleService = parkingLogRoleService;
        _reservationRepository = reservationRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<IEnumerable<GetActiveParkingSessionResponse>>> Handle(
        GetActiveParkingSessionQuery request,
        CancellationToken cancellationToken)
    {
        var sysSettings = SystemSettingsStore.Current;
        var effectiveCapacity = sysSettings.TotalCapacity > 0
            ? sysSettings.TotalCapacity
            : (request.ParkingCapacity > 0 ? request.ParkingCapacity : 500);

        var cacheKey = CacheKeys.ActiveSessions(effectiveCapacity);

        async Task<List<GetActiveParkingSessionResponse>> LoadAsync()
        {
            var logs = await _parkingLogRepository
                .GetActiveParkingLogsAsync(Math.Max(1000, effectiveCapacity));



            var activeLogs = logs
                .Where(x => x.EntryTime != default)
                .ToList();

            var dtos = new List<GetActiveParkingSessionResponse>();
            var graceMin = sysSettings.IsGracePeriodEnabled ? sysSettings.GracePeriodMinutes : 0;

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
                if (log.Vehicle == null)
                    continue;

                var isPersonnelParking = PersonnelParkingPolicy.AppliesTo(log.Vehicle.Owner?.UserProfile?.Personnel);
                var nowUtc = DateTime.UtcNow;
                var overstayHours = 0d;
                var amount = 0m;
                DateTime? maximumExitTimeUtc = null;
                var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);

                var userReservations = reservationsByOwner[log.Vehicle.OwnerId].ToList();
                var phEntryDate = philippinesEntry.Date;
                var phNowDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc).Date;

                bool IsReservationDateMatch(ParkFlow.Domain.Entities.ParkingReservation res)
                {
                    var resDate = res.ReservationDate.Date;
                    var phResDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(res.ReservationDate).Date;
                    return resDate == phEntryDate || phResDate == phEntryDate || resDate == phNowDate || phResDate == phNowDate;
                }

                var entryReservation = userReservations.FirstOrDefault(r =>
                    (r.VehicleId == log.VehicleId || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
                    IsReservationDateMatch(r) &&
                    (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Completed))
                    ?? userReservations.FirstOrDefault(r =>
                        IsReservationDateMatch(r) &&
                        (r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Completed))
                    ?? userReservations.FirstOrDefault(r =>
                        (r.VehicleId == log.VehicleId || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
                        IsReservationDateMatch(r) &&
                        r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Rejected)
                    ?? userReservations.FirstOrDefault(r =>
                        IsReservationDateMatch(r) &&
                        r.Status != ReservationStatus.Cancelled && r.Status != ReservationStatus.Rejected);

                if (isPersonnelParking)
                {
                    maximumExitTimeUtc = PersonnelParkingPolicy.GetDeadlineUtc(log.EntryTime, sysSettings);
                }
                else if (entryReservation != null)
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

                    if (log.EntryMethod != EntryMethod.Manual)
                    {
                        var schedules = verifiedCor != null
                            ? schedulesBySubmission[verifiedCor.Id].ToList()
                            : new List<ParkFlow.Domain.Entities.ParkingSchedule>();

                        if (!schedules.Any())
                        {
                            var userCor = relevantCor.Where(cor => cor.UserAccountId == log.Vehicle.OwnerId)
                                .OrderByDescending(cor => cor.CreatedAt).ToList();
                            var latest = userCor.FirstOrDefault();
                            schedules = latest == null ? [] : schedulesBySubmission[latest.Id].ToList();
                            if (schedules.Count == 0)
                                schedules = userCor.SelectMany(cor => schedulesBySubmission[cor.Id])
                                    .OrderByDescending(schedule => schedule.CreatedAt)
                                    .GroupBy(schedule => schedule.DayOfWeek).Select(group => group.First()).ToList();
                        }

                        var todaySchedules = schedules
                            .Where(s => s.DayOfWeek == philippinesEntry.DayOfWeek)
                            .OrderBy(s => s.StartTime)
                            .ToList();

                        var todaySchedule = todaySchedules.FirstOrDefault(s => philippinesEntry.TimeOfDay >= s.StartTime && philippinesEntry.TimeOfDay <= s.EndTime)
                            ?? todaySchedules.LastOrDefault();

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

                if (isPersonnelParking)
                {
                    maximumExitTimeUtc = PersonnelParkingPolicy.GetDeadlineUtc(log.EntryTime, sysSettings);
                    overstayHours = PersonnelParkingPolicy.GetChargeDuration(log.EntryTime, nowUtc, sysSettings).TotalHours;
                    amount = PersonnelParkingPolicy.CalculateCharge(log.EntryTime, nowUtc, sysSettings);
                }
                else if (log.EntryMethod == EntryMethod.Manual)
                {
                    var entryMidnightUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(23, 59, 59));
                    maximumExitTimeUtc = entryMidnightUtc;

                    var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc);
                    var overdueDays = (philippinesNow.Date - philippinesEntry.Date).Days;
                    if (overdueDays < 0) overdueDays = 0;

                    amount = 20m + (overdueDays * 100m);
                    overstayHours = overdueDays > 0 ? (nowUtc - entryMidnightUtc).TotalHours : 0;
                }
                else if (entryReservation != null)
                {
                    var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc);
                    var overdueDays = (philippinesNow.Date - philippinesEntry.Date).Days;
                    if (overdueDays < 0) overdueDays = 0;

                    if (maximumExitTimeUtc.HasValue && nowUtc > maximumExitTimeUtc.Value)
                    {
                        var overstayDuration = nowUtc - maximumExitTimeUtc.Value;
                        overstayHours = overstayDuration.TotalHours;
                        amount = 20m + 100m + (overdueDays * 100m);
                    }
                    else
                    {
                        amount = 20m;
                        overstayHours = 0;
                    }
                }
                else
                {
                    // If no schedule or reservation was found for entry day, apply default campus closing time (10:00 PM)
                    if (maximumExitTimeUtc == null)
                    {
                        var defaultClosingUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesEntry, new TimeSpan(22, 0, 0));
                        maximumExitTimeUtc = defaultClosingUtc > log.EntryTime ? defaultClosingUtc : log.EntryTime.AddHours(4);
                    }

                    if (maximumExitTimeUtc.HasValue && nowUtc > maximumExitTimeUtc.Value)
                    {
                        var overstayDuration = nowUtc - maximumExitTimeUtc.Value;
                        overstayHours = overstayDuration.TotalHours;
                        amount = _violationService.CalculatePenalty(overstayDuration);
                    }
                }

                var ownerProfile = log.Vehicle.Owner?.UserProfile;
                var ownerPhoneNumber = ownerProfile?.UserAccount?.PhoneNumber
                    ?? log.Vehicle.Owner?.PhoneNumber
                    ?? string.Empty;

                if (ownerProfile == null)
                {
                    continue;
                }

                var ownerEmail = log.Vehicle.Owner?.PrimaryEmail
                    ?? log.Vehicle.Owner?.AuthIdentities?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Email))?.Email
                    ?? string.Empty;

                var student = ownerProfile.Student;
                var personnel = ownerProfile.Personnel;
                var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);
                var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

                var totalHours = Math.Max(0, (nowUtc - log.EntryTime).TotalHours);

                dtos.Add(new GetActiveParkingSessionResponse(
                    FirstName: ownerProfile.FirstName,
                    LastName: ownerProfile.LastName,
                    MiddleName: ownerProfile.MiddleName,
                    PhoneNumber: ownerPhoneNumber,
                    Role: roleDetails.Role,
                    Email: ownerEmail,

                    Status: overstayHours > 0 ? "Overstay" : log.Status.ToString(),
                    PlateNumber: log.Vehicle.PlateNumber,
                    Brand: log.Vehicle.Brand,
                    VehicleType: log.Vehicle.VehicleType.ToString(),

                    EntryTime: log.EntryTime,
                    MaximumExitTime: maximumExitTimeUtc ?? default,
                    OverstayHours: overstayHours,
                    Amount: amount,

                    TotalParkingHours: $"{totalHours:F2} hours",
                    EntryMethod: log.EntryMethod.ToString()
                ));
            }

            return dtos;
        }
        var dtos = _cacheService != null
            ? await _cacheService.GetOrCreateAsync(cacheKey, LoadAsync, TimeSpan.FromSeconds(20), cancellationToken)
            : await LoadAsync();

        return Result<IEnumerable<GetActiveParkingSessionResponse>>
            .Success(dtos, "Active parking sessions retrieved.");
    }
}
