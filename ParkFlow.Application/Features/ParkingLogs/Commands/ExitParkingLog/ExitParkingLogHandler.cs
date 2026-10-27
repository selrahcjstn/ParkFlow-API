using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;

public class ExitParkingLogHandler : IRequestHandler<ExitParkingLogCommand, Result<ExitParkingLogResponse>>
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
    private readonly IViolationRepository _violationRepository;
    private readonly IParkingService _parkingService;
    private readonly IViolationService _violationService;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly IValidator<ExitParkingLogCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly IUserAccountRepository? _userAccountRepository;
    private readonly IEmailService? _emailService;
    private readonly INotificationService? _notificationService;
    private readonly ICacheService? _cacheService;

    public ExitParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IStudentRepository studentRepository,
        IPersonnelRepository personnelRepository,
        IAdminRepository adminRepository,
        IViolationRepository violationRepository,
        IParkingService parkingService,
        IViolationService violationService,
        IParkingLogRoleService parkingLogRoleService,
        IValidator<ExitParkingLogCommand> validator,
        ISignalRNotificationSender notificationSender,
        IParkingReservationRepository reservationRepository,
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
        _violationRepository = violationRepository;
        _parkingService = parkingService;
        _violationService = violationService;
        _parkingLogRoleService = parkingLogRoleService;
        _validator = validator;
        _notificationSender = notificationSender;
        _reservationRepository = reservationRepository;
        _userAccountRepository = userAccountRepository;
        _emailService = emailService;
        _notificationService = notificationService;
        _cacheService = cacheService;
    }

    public async Task<Result<ExitParkingLogResponse>> Handle(ExitParkingLogCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ExitParkingLogResponse>.Failure(errors, ErrorCode.BadRequest);
        }

        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null && _reservationRepository != null)
        {
            var reservationPass = await _reservationRepository.GetByReferenceNumberAsync(request.QrCodeHash);
            if (reservationPass != null)
            {
                if (reservationPass.VehicleId.HasValue)
                {
                    vehicle = await _vehicleRepository.GetByIdAsync(reservationPass.VehicleId.Value);
                }
                else
                {
                    var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(reservationPass.UserId);
                    var matchingVehicles = userVehicles.Where(v => (v.VehicleType == VehicleType.Car ? VehicleType.Car : VehicleType.Motorcycle) == reservationPass.ReservedVehicleType).ToList();
                    vehicle = matchingVehicles.FirstOrDefault(v => v.IsPrimary) ?? matchingVehicles.FirstOrDefault();
                }
            }
        }

        if (vehicle == null)
        {
            var studentNumber = ExtractStudentNumber(request.QrCodeHash);
            if (!string.IsNullOrEmpty(studentNumber))
            {
                var scannedStudent = await _studentRepository.GetByStudentNumberAsync(studentNumber);
                if (scannedStudent != null)
                {
                    var profile = scannedStudent.UserProfile ?? await _userProfileRepository.GetByIdAsync(scannedStudent.UserProfileId);
                    if (profile != null)
                    {
                        var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(profile.UserAccountId);
                        vehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();
                    }
                }
            }
        }

        if (vehicle == null)
            return Result<ExitParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        Guard? guard = null;
        Admin? staffAdmin = null;

        if (userProfile != null)
        {
            guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
            staffAdmin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
        }

        if (guard == null && staffAdmin == null)
        {
            if (_userAccountRepository != null && request.UserId != Guid.Empty)
            {
                var account = await _userAccountRepository.GetByIdAsync(request.UserId);
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

        var userReservations = _reservationRepository != null
            ? (await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId)).ToList()
            : new List<ParkFlow.Domain.Entities.ParkingReservation>();
        bool IsReservationDateMatch(ParkFlow.Domain.Entities.ParkingReservation res)
        {
            return ReservationSessionWindow.CoversEntry(res, active.EntryTime, SystemSettingsStore.Current);
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
        if (!await _parkingLogRepository.CompleteExitAsync(active, completedReservations, cancellationToken))
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
            : (staffAdmin != null ? "Campus Administrator" : "Campus Security");

        var response = new ExitParkingLogResponse
        {
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

    private static string? ExtractStudentNumber(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var trimmed = input.Trim();
        var delimiters = new[] { ',', '\n', '\r', ';', '|' };
        if (delimiters.Any(d => trimmed.Contains(d)))
        {
            var parts = trimmed.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 1)
            {
                return CleanStudentNumber(parts[0]);
            }
        }
        return CleanStudentNumber(trimmed);
    }

    private static string CleanStudentNumber(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return string.Empty;
        var cleaned = token.Trim();
        var colonIdx = cleaned.IndexOf(':');
        if (colonIdx >= 0 && colonIdx < cleaned.Length - 1)
        {
            var prefix = cleaned.Substring(0, colonIdx).ToLowerInvariant();
            if (prefix.Contains("student") || prefix.Contains("id") || prefix.Contains("no"))
            {
                cleaned = cleaned.Substring(colonIdx + 1).Trim();
            }
        }
        return cleaned;
    }
}
