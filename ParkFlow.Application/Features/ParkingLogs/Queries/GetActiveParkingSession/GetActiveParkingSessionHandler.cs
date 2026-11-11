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
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly ICacheService? _cacheService;
    private readonly IVisitorRepository? _visitorRepository;

    public GetActiveParkingSessionHandler(
        IParkingLogRepository parkingLogRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService,
        IParkingReservationRepository? reservationRepository = null,
        ICacheService? cacheService = null,
        IVisitorRepository? visitorRepository = null)
    {
        _visitorRepository = visitorRepository;
        _parkingLogRepository = parkingLogRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _corSubmissionRepository = corSubmissionRepository;
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
                var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime);
                var timing = ActiveSessionTiming.ResolveActive(log, nowUtc, sysSettings, corSubmissions,
                    schedulesBySubmission, reservationsByOwner[log.Vehicle.OwnerId]);
                var maximumExitTimeUtc = timing.MaximumExitTime;
                var overstayHours = timing.OverstayHours;

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
                    MaximumExitTime: PersonnelParkingPolicy.IsFaculty(personnel) ? null : maximumExitTimeUtc,
                    OverstayHours: overstayHours,
                    TotalParkingHours: $"{totalHours:F2} hours",
                    EntryMethod: log.EntryMethod.ToString()
                ));
            }

            return dtos;
        }
        var dtos = _cacheService != null
            ? await _cacheService.GetOrCreateAsync(cacheKey, LoadAsync, TimeSpan.FromSeconds(20), cancellationToken)
            : await LoadAsync();

        // Visitors are read live (not from the 20s cache) so a fresh entry shows right away.
        var all = new List<GetActiveParkingSessionResponse>(dtos);
        if (_visitorRepository != null)
        {
            var visits = await _visitorRepository.GetActiveVisitSessionsAsync(Math.Max(1000, effectiveCapacity), cancellationToken);
            var nowUtc = DateTime.UtcNow;
            foreach (var visit in visits)
            {
                var fullName = (visit.Visitor?.FullName ?? "Visitor").Trim();
                var space = fullName.IndexOf(' ');
                var first = space < 0 ? fullName : fullName[..space];
                var last = space < 0 ? string.Empty : fullName[(space + 1)..].Trim();
                var vehicle = visit.VisitorVehicle;
                all.Add(new GetActiveParkingSessionResponse(
                    FirstName: first, LastName: last, MiddleName: null,
                    PhoneNumber: visit.Visitor?.ContactNumber ?? string.Empty,
                    Role: "Visitor", Email: null,
                    Status: "Inside",
                    PlateNumber: string.IsNullOrWhiteSpace(vehicle?.PlateNumber) ? null : vehicle!.PlateNumber,
                    Brand: vehicle?.Brand ?? string.Empty,
                    VehicleType: (vehicle?.VehicleType ?? visit.Visitor?.VehicleType ?? default).ToString(),
                    EntryTime: visit.EntryTime, MaximumExitTime: null, OverstayHours: 0,
                    TotalParkingHours: $"{Math.Max(0, (nowUtc - visit.EntryTime).TotalHours):F2} hours",
                    EntryMethod: "Visitor"));
            }
            all = all.OrderByDescending(x => x.EntryTime).ToList();
        }

        return Result<IEnumerable<GetActiveParkingSessionResponse>>
            .Success(all, "Active parking sessions retrieved.");
    }
}
