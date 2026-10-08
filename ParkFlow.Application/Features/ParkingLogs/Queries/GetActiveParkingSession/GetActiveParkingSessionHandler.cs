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
                var amount = 0m;
                var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
                var timing = ActiveSessionTiming.Resolve(log, nowUtc, sysSettings, corSubmissions,
                    schedulesBySubmission, reservationsByOwner[log.Vehicle.OwnerId]);
                var maximumExitTimeUtc = timing.DeadlineUtc;
                var overstayHours = ActiveSessionTiming.GetOverstayHours(log, nowUtc, sysSettings, maximumExitTimeUtc);

                if (isPersonnelParking)
                {
                    amount = PersonnelParkingPolicy.CalculateCharge(log.EntryTime, nowUtc, sysSettings);
                }
                else if (log.EntryMethod == EntryMethod.Manual)
                {
                    var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc);
                    var overdueDays = (philippinesNow.Date - philippinesEntry.Date).Days;
                    if (overdueDays < 0) overdueDays = 0;

                    amount = 20m + (overdueDays * 100m);
                }
                else if (timing.HasReservation)
                {
                    var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc);
                    var overdueDays = (philippinesNow.Date - philippinesEntry.Date).Days;
                    if (overdueDays < 0) overdueDays = 0;

                    if (overstayHours > 0)
                    {
                        amount = 20m + 100m + (overdueDays * 100m);
                    }
                    else
                    {
                        amount = 20m;
                    }
                }
                else
                {
                    if (overstayHours > 0)
                    {
                        var overstayDuration = nowUtc - maximumExitTimeUtc;
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
                    MaximumExitTime: maximumExitTimeUtc,
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
