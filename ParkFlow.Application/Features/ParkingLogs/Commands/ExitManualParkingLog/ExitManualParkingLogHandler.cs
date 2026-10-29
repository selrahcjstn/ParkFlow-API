using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;

public class ExitManualParkingLogHandler : IRequestHandler<ExitManualParkingLogCommand, Result<ExitParkingLogResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPersonnelRepository _personnelRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly IParkingService _parkingService;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly IValidator<ExitManualParkingLogCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IEmailService? _emailService;
    private readonly INotificationService? _notificationService;
    private readonly ICacheService? _cacheService;

    public ExitManualParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,

        IParkingService parkingService,

        IParkingLogRoleService parkingLogRoleService,
        IValidator<ExitManualParkingLogCommand> validator,
        ISignalRNotificationSender notificationSender,
        IParkingReservationRepository? reservationRepository = null,
        IUserAccountRepository? userAccountRepository = null,
        IEmailService? emailService = null,
        INotificationService? notificationService = null,
        ICacheService? cacheService = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _studentRepository = studentRepository;
        _personnelRepository = personnelRepository;
        _adminRepository = adminRepository;
        _parkingService = parkingService;
        _parkingLogRoleService = parkingLogRoleService;
        _validator = validator;
        _notificationSender = notificationSender;
        _reservationRepository = reservationRepository;
        _userAccountRepository = userAccountRepository;
        _emailService = emailService;
        _notificationService = notificationService;
        _cacheService = cacheService;
    }

    public async Task<Result<ExitParkingLogResponse>> Handle(ExitManualParkingLogCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ExitParkingLogResponse>.Failure(errors, ErrorCode.BadRequest);
        }

        var vehicle = await _vehicleRepository.GetByPlateNumberAsync(request.PlateNumber);

        if (vehicle == null)
            return Result<ExitParkingLogResponse>.Failure("Vehicle not found.", ErrorCode.NotFound);

        var effectiveUserId = request.UserId ?? Guid.Empty;
        UserProfile? userProfile = null;
        Guard? guard = null;
        Admin? staffAdmin = null;

        if (effectiveUserId != Guid.Empty)
        {
            userProfile = await _userProfileRepository.GetByUserIdAsync(effectiveUserId);
            if (userProfile != null)
            {
                guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
                staffAdmin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
            }
        }

        if (guard == null && staffAdmin == null)
        {
            if (effectiveUserId != Guid.Empty && _userAccountRepository != null)
            {
                var account = await _userAccountRepository.GetByIdAsync(effectiveUserId);
                if (account?.UserProfile != null)
                {
                    userProfile = account.UserProfile;
                    guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
                    staffAdmin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
                }
            }

            if (guard == null && staffAdmin == null)
            {
                var allAdmins = await _adminRepository.ListAllAsync();
                staffAdmin = allAdmins.FirstOrDefault();
                if (staffAdmin != null && userProfile == null)
                {
                    userProfile = staffAdmin.UserProfile;
                }
            }
        }

        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (active == null)
            return Result<ExitParkingLogResponse>.Failure("No active parking log found for this vehicle.", ErrorCode.NotFound);

        var statusBeforeExit = active.Status;
        var exitTime = DateTime.UtcNow;

        _parkingService.MarkExit(active);
        var completedReservations = new List<ParkingReservation>();

        var corSubmissions = await _corSubmissionRepository.GetByUserIdsAsync([vehicle.OwnerId]);
        var verifiedCor = corSubmissions.FirstOrDefault(c => c.UserAccountId == vehicle.OwnerId && c.VerificationStatus == CorVerificationStatus.Verified);

        var endTime = exitTime;
        DateTime? maximumExitTime = null;
        double overstayTime = 0;

        var philippinesEntry = ParkingTimeHelper.ConvertUtcToPhilippinesTime(active.EntryTime);
        var philippinesExit = ParkingTimeHelper.ConvertUtcToPhilippinesTime(exitTime);

        var userReservations = _reservationRepository != null ? (await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId)).ToList() : [];
        bool IsReservationDateMatch(ParkFlow.Domain.Entities.ParkingReservation res)
        {
            return ReservationSessionWindow.CoversEntry(res, active.EntryTime);
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

        var employeeProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        var employeePersonnel = employeeProfile == null ? null : await _personnelRepository.GetByUserProfileIdAsync(employeeProfile.Id);
        var isPersonnelParking = PersonnelParkingPolicy.AppliesTo(employeePersonnel);
        var systemSettings = SystemSettingsStore.Current;

        var entrySchedules = verifiedCor == null || isPersonnelParking || active.EntryMethod == EntryMethod.Manual || entryReservation != null
            ? [] : (await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id)).ToList();
        var checkoutTiming = ActiveSessionTiming.ResolveCheckout(active.EntryTime, active.EntryMethod, employeePersonnel,
            entryReservation, ScheduleService.FindEntrySchedule(entrySchedules, philippinesEntry), systemSettings, exitTime);
        maximumExitTime = checkoutTiming.MaximumExitTime;
        overstayTime = checkoutTiming.OverstayHours;

        if (_reservationRepository != null)
        {
            var activeTodayReservations = userReservations.Where(r =>
                (r.VehicleId == vehicle.Id || r.VehicleId == null || r.UserId == vehicle.OwnerId) &&
                (r.ReservationDate.Date == philippinesEntry.Date || r.ReservationDate.Date == philippinesExit.Date) &&
                r.Status == ReservationStatus.Approved).ToList();

            if (entryReservation != null && !activeTodayReservations.Any(r => r.Id == entryReservation.Id))
            {
                activeTodayReservations.Add(entryReservation);
            }

            foreach (var res in activeTodayReservations)
            {
                res.MarkCompleted();
                completedReservations.Add(res);
            }

        }

        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        if (ownerProfile == null)
            return Result<ExitParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);
        var charge = ParkingCharges.Checkout(active, vehicle, employeePersonnel,
            await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id) != null, entryReservation, overstayTime, exitTime);
        if (!await _parkingLogRepository.CompleteExitWithChargeAsync(active, completedReservations, charge, cancellationToken))
            return Result<ExitParkingLogResponse>.Failure("This session has already exited. Refresh the parking history.", ErrorCode.Conflict);
        if (completedReservations.Count > 0)
        {
            try
            {
                await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ReservationStatusChanged", new { status = "Completed", userId = vehicle.OwnerId });
                await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ReservationUpdated", new { status = "Completed", userId = vehicle.OwnerId });
                await _notificationSender.SendToAllAsync("ReservationListUpdated", new { });
            }
            catch { }
        }

        var actualExitTime = active.ExitTime ?? exitTime;

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var guardMiddle = userProfile != null && !string.IsNullOrWhiteSpace(userProfile.MiddleName) ? $" {userProfile.MiddleName}" : "";
        var guardName = userProfile != null
            ? $"{userProfile.FirstName}{guardMiddle} {userProfile.LastName}"
            : "Campus Administrator";

        var response = new ExitParkingLogResponse
        {
            PenaltyFee = charge?.PenaltyFee ?? 0m,
            IsViolation = charge != null,
            ViolationId = charge?.Id,
            ViolationType = charge?.ViolationType.ToString(),
            SettlementStatus = charge?.SettlementStatus.ToString() ?? "Settled",
            ReferenceNumber = charge?.ReferenceNumber,
            ProfilePictureUrl = ownerProfile.ProfilePictureUrl,
            VehiclePictureUrl = vehicle.VehiclePictureUrl,
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            MiddleName = ownerProfile.MiddleName,
            Role = roleDetails.Role,
            Status = active.Status.ToString(),
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            VehicleType = vehicle.VehicleType.ToString(),
            EntryTime = active.EntryTime,
            ExitTime = actualExitTime,
            OverstayTime = overstayTime,
            GuardName = guardName,
            IssuedBy = guardName
        };

        if (vehicle.OwnerId != Guid.Empty)
        {
            if (_notificationService != null)
                await _notificationService.CreateAndSendNotificationAsync(vehicle.OwnerId, "Vehicle Exit Recorded",
                    $"Vehicle [{vehicle.PlateNumber}] exit confirmed at campus gate. Issued by {guardName}.",
                    type: "approved", subtitle: $"Issued by: {guardName}", vehiclePlate: vehicle.PlateNumber,
                    actionRoute: "/(users)/(tabs)/history", actionText: "View Log History", issuer: guardName,
                    driverName: $"{ownerProfile.FirstName} {ownerProfile.LastName}".Trim(), driverRole: roleDetails.Role,
                    vehicleBrand: vehicle.Brand, signalRData: response);
            try
            {
                await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ParkingSessionUpdated", response);
                await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ExitResponse", response);
                await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ReceiveParkingReceipt", response);
                await _notificationSender.SendToAllAsync("ParkingOccupancyUpdated", new { plateNumber = vehicle.PlateNumber, action = "CheckOut" });
                await _notificationSender.SendToAllAsync("ParkingSessionUpdated", response);
            }
            catch { /* Realtime delivery must not undo a persisted checkout. */ }
            if (_emailService != null && _userAccountRepository != null)
            {
                try
                {
                    var account = await _userAccountRepository.GetByIdAsync(vehicle.OwnerId);
                    if (!string.IsNullOrWhiteSpace(account?.PrimaryEmail))
                        await _emailService.SendEmailAsync(account.PrimaryEmail, "ParkFlow - Vehicle Exit Recorded",
                            $"Vehicle [{vehicle.PlateNumber}] exited at {ParkingTimeHelper.ConvertUtcToPhilippinesTime(actualExitTime):MMMM dd, yyyy hh:mm tt}. Your parking session has ended.");
                }
                catch { /* Email delivery must not undo a persisted checkout. */ }
            }
        }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix, cancellationToken);
        }

        return Result<ExitParkingLogResponse>.Success(response, "Exit Confirmed");
    }
}
