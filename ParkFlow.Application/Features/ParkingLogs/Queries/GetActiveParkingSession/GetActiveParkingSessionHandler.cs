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

    public GetActiveParkingSessionHandler(
        IParkingLogRepository parkingLogRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IViolationService violationService,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService,
        IParkingReservationRepository? reservationRepository = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _violationService = violationService;
        _adminRepository = adminRepository;
        _parkingLogRoleService = parkingLogRoleService;
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<IEnumerable<GetActiveParkingSessionResponse>>> Handle(
        GetActiveParkingSessionQuery request,
        CancellationToken cancellationToken)
    {
        var logs = await _parkingLogRepository
            .GetActiveParkingLogsAsync(request.ParkingCapacity);

        var corSubmissions = await _corSubmissionRepository
            .ListCorSubmissionsAsync();

        var activeLogs = logs
            .Where(x => x.EntryTime != default)
            .ToList();

        var dtos = new List<GetActiveParkingSessionResponse>();
        var sysSettings = SystemSettingsStore.Current;
        var graceMin = sysSettings.IsGracePeriodEnabled ? sysSettings.GracePeriodMinutes : 0;

        foreach (var log in activeLogs)
        {
            var nowUtc = DateTime.UtcNow;
            var overstayHours = 0d;
            var amount = 0m;
            DateTime? maximumExitTimeUtc = null;
            var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);

            var userReservations = _reservationRepository != null ? await _reservationRepository.GetByUserIdAsync(log.Vehicle.OwnerId) : [];
            var entryReservation = userReservations.FirstOrDefault(r =>
                (r.VehicleId == log.VehicleId || r.VehicleId == null) &&
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
                    var schedules = await _parkingScheduleRepository
                        .GetBySubmissionIdAsync(verifiedCor.Id);

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

            var ownerProfile = log.Vehicle.Owner?.UserProfile;
            var ownerPhoneNumber = ownerProfile?.UserAccount?.PhoneNumber;

            if (ownerProfile == null || string.IsNullOrWhiteSpace(ownerPhoneNumber))
            {
                continue;
            }

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

                Status: log.Status.ToString(),
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

        return Result<IEnumerable<GetActiveParkingSessionResponse>>
            .Success(dtos, "Active parking sessions retrieved.");
    }
}