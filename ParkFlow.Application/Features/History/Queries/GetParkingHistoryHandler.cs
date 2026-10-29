using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.History.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.History.Queries;

public class GetParkingHistoryHandler : IRequestHandler<GetParkingHistoryQuery, Result<PagedParkingHistoryResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly IViolationRepository? _violationRepository;

    public GetParkingHistoryHandler(
        IParkingLogRepository parkingLogRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IParkingReservationRepository? reservationRepository = null,
        IViolationRepository? violationRepository = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _parkingLogRoleService = parkingLogRoleService;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _reservationRepository = reservationRepository;
        _violationRepository = violationRepository;
    }

    public async Task<Result<PagedParkingHistoryResponse>> Handle(GetParkingHistoryQuery request, CancellationToken cancellationToken)
    {
        Guid? filterUserId = request.UserId;

        if (request.UserId != Guid.Empty)
        {
            var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
            if (profile == null)
                return Result<PagedParkingHistoryResponse>.Failure("User profile not found.", ErrorCode.NotFound);

            var guard = await _guardRepository.GetByUserProfileIdAsync(profile.Id);
            var isGuard = guard != null;

            if (isGuard)
            {
                filterUserId = null;
            }
        }
        else
        {
            filterUserId = null;
        }

        var logs = await _parkingLogRepository.GetParkingHistoryAsync(
            filterUserId,
            request.PageNumber,
            request.PageSize);

        var ownerIds = logs.Where(l => l.Vehicle != null).Select(l => l.Vehicle.OwnerId).ToHashSet();
        var corSubmissions = (await _corSubmissionRepository.ListCorSubmissionsAsync())
            .Where(c => ownerIds.Contains(c.UserAccountId)).ToList();
        var schedules = (await _parkingScheduleRepository.GetBySubmissionIdsAsync(corSubmissions.Select(c => c.Id)))
            .ToLookup(s => s.SubmissionId);
        var settings = SystemSettingsStore.Current;
        var reservations = _reservationRepository == null ? [] :
            (await _reservationRepository.GetByUserIdsAsync(ownerIds)).ToList();

        var dtoList = new List<ParkingHistoryResponse>();
        foreach (var log in logs)
        {
            // Completed amounts always come from persisted records, never today's rates.
            var charge = _violationRepository == null ? null : await _violationRepository.GetByLogIdAsync(log.Id);
            var vehicle = log.Vehicle;
            var ownerProfile = vehicle?.Owner?.UserProfile;
            var student = ownerProfile?.Student;
            var personnel = ownerProfile?.Personnel;
            var admin = ownerProfile != null ? await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id) : null;
            var roleDetails = ownerProfile != null
                ? _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin)
                : null;

            // Use the actual exit time from DB — null means the session is still active
            var exitTimeVal = log.ExitTime ?? log.UpdatedAt;
            var duration = exitTimeVal.HasValue
                ? Math.Round((exitTimeVal.Value - log.EntryTime).TotalHours, 2)
                : (double?)null;

            var overstayHours = 0d;
            if (vehicle != null && exitTimeVal.HasValue)
            {
                var timing = ActiveSessionTiming.Resolve(log, exitTimeVal.Value, settings, corSubmissions,
                    schedules, reservations.Where(r => r.UserId == vehicle.OwnerId));
                if (!timing.IsSpecialReservation)
                    overstayHours = Math.Round(ActiveSessionTiming.GetOverstayHours(log, exitTimeVal.Value, settings, timing.DeadlineUtc), 2);
            }

            var ownerEmail = vehicle?.Owner?.PrimaryEmail
                ?? vehicle?.Owner?.AuthIdentities?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.Email))?.Email
                ?? string.Empty;

            var firstName = ownerProfile?.FirstName ?? (string.IsNullOrWhiteSpace(vehicle?.PlateNumber) ? "Registered" : "Guest");
            var lastName = ownerProfile?.LastName ?? (string.IsNullOrWhiteSpace(vehicle?.PlateNumber) ? "Driver" : "Visitor");
            var roleName = !string.IsNullOrWhiteSpace(roleDetails?.Role) ? roleDetails.Role : "Client";
            var plateNum = vehicle?.PlateNumber ?? "N/A";
            var brand = vehicle?.Brand ?? "—";
            var vehType = vehicle?.VehicleType.ToString() ?? "Car";

            dtoList.Add(new ParkingHistoryResponse
            {
                Amount = charge?.PenaltyFee ?? 0m,
                PenaltyFee = charge?.PenaltyFee ?? 0m,
                ViolationFee = charge?.PenaltyFee ?? 0m,
                HasViolation = charge?.ViolationType is ViolationType.Overstay or ViolationType.UnauthorizedParking or ViolationType.ParkingInRestrictedArea,
                IsPaid = charge == null || charge.SettlementStatus == SettlementStatus.Settled,
                ReferenceNumber = charge?.ReferenceNumber,
                SessionId = log.Id,
                EntryMethod = log.EntryMethod.ToString(),
                FirstName = firstName,
                LastName = lastName,
                MiddleName = ownerProfile?.MiddleName,
                Email = ownerEmail,
                RoleName = roleName,
                PlateNumber = plateNum,
                Brand = brand,
                Type = vehType,
                EntryTime = log.EntryTime,
                ExitTime = exitTimeVal,
                ParkingDuration = duration,
                TotalParkingHours = duration,
                OverstayHours = overstayHours,
                Status = log.ExitTime.HasValue ? "Completed" : "Parked"
            });
        }

        var response = new PagedParkingHistoryResponse
        {
            GeneratedAt = DateTime.UtcNow,
            Items = dtoList
        };

        return Result<PagedParkingHistoryResponse>.Success(response, "Parking history retrieved successfully.");
    }
}
