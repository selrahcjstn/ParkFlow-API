using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Violations.Queries.GetViolationHistory;

public class GetViolationHistoryHandler
    : IRequestHandler<GetViolationHistoryQuery, Result<PagedViolationHistoryResponse>>
{
    private readonly IViolationRepository _violationRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly IParkingLogRepository? _parkingLogRepository;
    private readonly ICorSubmissionRepository? _corSubmissionRepository;
    private readonly IParkingScheduleRepository? _parkingScheduleRepository;
    private readonly IParkingReservationRepository? _reservationRepository;

    public GetViolationHistoryHandler(
        IViolationRepository violationRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IAdminRepository adminRepository,
        IParkingLogRoleService parkingLogRoleService,
        IParkingLogRepository? parkingLogRepository = null,
        ICorSubmissionRepository? corSubmissionRepository = null,
        IParkingScheduleRepository? parkingScheduleRepository = null,
        IParkingReservationRepository? reservationRepository = null)
    {
        _violationRepository = violationRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _adminRepository = adminRepository;
        _parkingLogRoleService = parkingLogRoleService;
        _parkingLogRepository = parkingLogRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<PagedViolationHistoryResponse>> Handle(
        GetViolationHistoryQuery request,
        CancellationToken cancellationToken)
    {
        // Determine if caller is a guard or admin — guards and admins see all violations
        var profile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        if (profile is null)
            return Result<PagedViolationHistoryResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(profile.Id);
        var callerAdmin = await _adminRepository.GetByUserProfileIdAsync(profile.Id);
        var canViewAll = guard is not null || callerAdmin is not null;

        if (canViewAll && _parkingLogRepository != null && _corSubmissionRepository != null && _parkingScheduleRepository != null)
        {
            try
            {
                var activeLogs = await _parkingLogRepository.GetActiveParkingLogsAsync(100);
                var parkedLogs = activeLogs.Where(l => l.EntryTime != default && l.Status == ParkingStatus.Parked && l.ExitTime == null && l.Vehicle != null).ToList();
                if (parkedLogs.Count > 0)
                {
                    var nowUtc = DateTime.UtcNow;
                    var sysSettings = SystemSettingsStore.Current;
                    var ownerIds = parkedLogs.Select(l => l.Vehicle.OwnerId).Where(id => id != Guid.Empty).Distinct().ToArray();
                    var corSubmissions = (await _corSubmissionRepository.GetByUserIdsAsync(ownerIds)).ToList();
                    var schedulesBySubmission = (await _parkingScheduleRepository.GetBySubmissionIdsAsync(corSubmissions.Select(c => c.Id)))
                        .ToLookup(s => s.SubmissionId);
                    var reservationsByOwner = (_reservationRepository == null
                        ? new List<ParkingReservation>()
                        : (await _reservationRepository.GetByUserIdsAsync(ownerIds)).ToList())
                        .ToLookup(r => r.UserId);

                    foreach (var log in parkedLogs)
                    {
                        var vehicle = log.Vehicle;
                        var owner = vehicle.Owner;
                        if (owner == null || owner.Id == Guid.Empty) continue;

                        var timing = ActiveSessionTiming.ResolveActive(
                            log, nowUtc, sysSettings, corSubmissions, schedulesBySubmission, reservationsByOwner[owner.Id]);

                        if (timing.MaximumExitTime == null) continue;

                        var isOverstaying = timing.OverstayHours > 0 || nowUtc >= timing.MaximumExitTime.Value;
                        if (!isOverstaying) continue;

                        var ownerProfile = owner.UserProfile;
                        var rate = sysSettings.ForVehicle(vehicle.VehicleType);
                        var days = Math.Max(0, (ParkingTimeHelper.ConvertUtcToPhilippinesTime(nowUtc).Date -
                            ParkingTimeHelper.ConvertUtcToPhilippinesTime(log.EntryTime).Date).Days);
                        var userReservation = reservationsByOwner[owner.Id].FirstOrDefault(r => r.VehicleId == vehicle.Id && r.Status == ReservationStatus.Approved);
                        var isPersonnel = ownerProfile?.Personnel != null;
                        var isFaculty = PersonnelParkingPolicy.IsFaculty(ownerProfile?.Personnel);

                        decimal fee;
                        if (userReservation?.Type == ReservationType.Special) fee = rate.VipEntryFee;
                        else if (isFaculty)
                            fee = rate.FacultyEntryFee + (decimal)Math.Ceiling(Math.Max(0, timing.OverstayHours)) * rate.ViolationRatePerHour;
                        else if (isPersonnel)
                            fee = rate.PersonnelEntryFee + (decimal)Math.Ceiling(Math.Max(0, timing.OverstayHours)) * rate.ViolationRatePerHour;
                        else if (log.EntryMethod == EntryMethod.Manual) fee = rate.EntryFee + days * rate.DailyOverstayFee;
                        else if (userReservation != null) fee = rate.EntryFee + (timing.OverstayHours > 0 ? (days + 1) * rate.DailyOverstayFee : 0);
                        else fee = (ownerProfile?.Student != null ? rate.StudentEntryFee : rate.EntryFee) +
                            new ViolationService().CalculatePenaltyForVehicle(TimeSpan.FromHours(Math.Max(0, timing.OverstayHours)), vehicle.VehicleType);

                        if (fee <= 0 && rate.ViolationRatePerHour > 0)
                        {
                            fee = rate.ViolationRatePerHour;
                        }

                        if (fee > 0)
                        {
                            var existing = await _violationRepository.GetByLogIdAsync(log.Id);
                            if (existing == null)
                            {
                                var newViolation = new Violation(log.Id, fee, ViolationType.Overstay);
                                await _violationRepository.AddAsync(newViolation);
                            }
                            else if (existing.SettlementStatus != SettlementStatus.Settled && existing.PenaltyFee != fee)
                            {
                                existing.UpdatePenaltyFee(fee);
                                await _violationRepository.UpdateAsync(existing);
                            }
                        }
                    }
                }
            }
            catch
            {
                // Non-fatal sync to allow query response
            }
        }

        var violations = await _violationRepository.GetViolationHistoryAsync(
            canViewAll ? null : request.UserId,
            request.PageNumber,
            request.PageSize,
            request.UnpaidOnly);

        var items = new List<ViolationHistoryResponse>();

        foreach (var violation in violations)
        {
            var log = violation.ParkingLog;
            var vehicle = log?.Vehicle;
            var visit = violation.VisitSession;
            var visitor = visit?.Visitor;
            var ownerProfile = vehicle?.Owner?.UserProfile;
            if (ownerProfile == null && vehicle?.OwnerId != null && vehicle.OwnerId != Guid.Empty)
            {
                ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
            }

            var admin = ownerProfile != null ? await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id) : null;
            var roleDetails = ownerProfile != null
                ? _parkingLogRoleService.GetRoleDetails(
                    ownerProfile,
                    ownerProfile.Student,
                    ownerProfile.Personnel,
                    admin)
                : null;

            items.Add(new ViolationHistoryResponse
            {
                // Owner
                FirstName = visitor?.FullName ?? ownerProfile?.FirstName ?? "Registered",
                LastName = visitor != null ? "" : ownerProfile?.LastName ?? "User",
                MiddleName = ownerProfile?.MiddleName,
                RoleName = visitor != null ? "Visitor" : roleDetails?.Role ?? "User",

                // Vehicle
                PlateNumber = violation.VisitSession?.VisitorVehicle?.PlateNumber ?? vehicle?.PlateNumber ?? "N/A",
                Brand = violation.VisitSession?.VisitorVehicle?.Brand ?? vehicle?.Brand ?? "N/A",
                VehicleType = violation.VisitSession?.VisitorVehicle?.VehicleType.ToString() ?? vehicle?.VehicleType.ToString() ?? "N/A",

                // Violation
                ViolationId = violation.Id,
                ReferenceNumber = violation.ReferenceNumber,
                ViolationType = FormatViolationType(violation.ViolationType),
                PenaltyFee = violation.PenaltyFee,
                SettlementStatus = violation.SettlementStatus.ToString(),
                IsPaid = violation.SettlementStatus == global::SettlementStatus.Settled,

                // Session
                EntryTime = visit?.EntryTime ?? log?.EntryTime ?? violation.CreatedAt,
                ExitTime = visit?.ExitTime ?? log?.ExitTime ?? DateTime.UtcNow,
                IssuedAt = violation.CreatedAt,
                PaidAt = violation.SettlementStatus == SettlementStatus.Settled ? violation.UpdatedAt : null
            });
        }

        var response = new PagedViolationHistoryResponse
        {
            GeneratedAt = DateTime.UtcNow,
            Items = items
        };

        return Result<PagedViolationHistoryResponse>.Success(response, "Violation history retrieved successfully.");
    }

    private static string FormatViolationType(ParkFlow.Domain.Enums.ViolationType type) => type switch
    {
        ParkFlow.Domain.Enums.ViolationType.ReservationCharge => "Reservation Parking Charge",
        ParkFlow.Domain.Enums.ViolationType.ManualParkingCharge => "Manual Parking Charge",
        ParkFlow.Domain.Enums.ViolationType.Overstay => "Parking Overstay",
        ParkFlow.Domain.Enums.ViolationType.NoPayment => "Unpaid Parking",
        ParkFlow.Domain.Enums.ViolationType.UnauthorizedParking => "Unauthorized Parking",
        ParkFlow.Domain.Enums.ViolationType.ParkingInRestrictedArea => "Parking in Restricted Area",
        _ => type.ToString()
    };
}
