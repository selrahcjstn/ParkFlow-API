using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Enums;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

public class CreateParkingLogHandler : IRequestHandler<CreateParkingLogCommand, Result<CreateParkingLogResponse>>
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
    private readonly IScheduleService _scheduleService;
    private readonly IParkingLogRoleService _parkingLogRoleService;
    private readonly ISignalRNotificationSender _signalRNotificationSender;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly INotificationService? _notificationService;
    private readonly ICacheService? _cacheService;

    public CreateParkingLogHandler(
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
        IScheduleService scheduleService,
        IParkingLogRoleService parkingLogRoleService,
        ISignalRNotificationSender signalRNotificationSender,
        IParkingReservationRepository? reservationRepository = null,
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
        _scheduleService = scheduleService;
        _parkingLogRoleService = parkingLogRoleService;
        _signalRNotificationSender = signalRNotificationSender;
        _reservationRepository = reservationRepository;
        _notificationService = notificationService;
        _cacheService = cacheService;
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateParkingLogCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByQrCodeHashAsync(request.QrCodeHash);

        if (vehicle == null && _reservationRepository != null)
        {
            // Try resolving by Reservation Reference Number
            var reservationPass = await _reservationRepository.GetByReferenceNumberAsync(request.QrCodeHash);
            if (reservationPass != null)
            {
                if (reservationPass.Status == ReservationStatus.Completed)
                {
                    return Result<CreateParkingLogResponse>.Failure("Entry denied: This reservation pass has already been used and is now void.", ErrorCode.Forbidden);
                }

                if (reservationPass.Status != ReservationStatus.Approved)
                {
                    return Result<CreateParkingLogResponse>.Failure($"Entry denied: Reservation status is {reservationPass.Status}.", ErrorCode.Forbidden);
                }

                var phNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
                if (reservationPass.ReservationDate.Date != phNow.Date)
                {
                    return Result<CreateParkingLogResponse>.Failure($"Entry denied: Reservation is for {reservationPass.ReservationDate:MMMM dd, yyyy}. Not valid today.", ErrorCode.Forbidden);
                }

                var sysSettings = SystemSettingsStore.Current;
                var earlyBuff = sysSettings.IsEarlyParkingAllowed ? sysSettings.EarlyParkingMinutes : 0;
                var earliestResPassEntry = reservationPass.StartTime.Subtract(TimeSpan.FromMinutes(earlyBuff));

                if (reservationPass.Type != ReservationType.Special && phNow.TimeOfDay > reservationPass.EndTime)
                {
                    return Result<CreateParkingLogResponse>.Failure($"Entry denied: Reservation schedule for today ended at {DateTime.Today.Add(reservationPass.EndTime):hh:mm tt}.", ErrorCode.Forbidden);
                }

                if (reservationPass.Type != ReservationType.Special && phNow.TimeOfDay < earliestResPassEntry)
                {
                    return Result<CreateParkingLogResponse>.Failure($"Entry denied: Too early for reservation. Earliest allowed entry is {DateTime.Today.Add(earliestResPassEntry):hh:mm tt}.", ErrorCode.BadRequest);
                }

                if (reservationPass.VehicleId.HasValue)
                {
                    vehicle = await _vehicleRepository.GetByIdAsync(reservationPass.VehicleId.Value);
                }
                else
                {
                    var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(reservationPass.UserId);
                    vehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();
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
            return Result<CreateParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        var activeViolationCount = await _violationRepository.GetActiveViolationCountAsync(vehicle.Id, vehicle.OwnerId);
        if (activeViolationCount > 0)
            return Result<CreateParkingLogResponse>.Failure("Entry denied. Please settle all unpaid violations before entering campus.", ErrorCode.Forbidden);

        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

        if (ownerProfile == null)
            return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        var activeParkingLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (activeParkingLog != null)
        {
            var conflictResponse = new CreateParkingLogResponse
            {
                FirstName = ownerProfile.FirstName,
                LastName = ownerProfile.LastName,
                MiddleName = ownerProfile.MiddleName,
                PlateNumber = vehicle.PlateNumber,
                Brand = vehicle.Brand,
                QrCodeHash = vehicle.QrCodeHash,
                VehicleType = vehicle.VehicleType.ToString(),
                EntryMethod = activeParkingLog.EntryMethod.ToString()
            };

            return Result<CreateParkingLogResponse>.Failure(
                conflictResponse,
                "Vehicle is already parked.",
                ErrorCode.Conflict);
        }

        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        Guard? guard = null;

        if (userProfile != null)
        {
            guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
        }
        else if (request.UserId != Guid.Empty)
        {
            userProfile = await _userProfileRepository.GetByIdAsync(request.UserId);
            if (userProfile != null)
            {
                guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
            }
        }

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var isStudentOrPersonnel = (student != null || personnel != null) && admin == null;

        DateTime? maximumExitTimeUtc = null;

        var userReservations = _reservationRepository != null ? await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId) : [];
        var utcNow = DateTime.UtcNow;
        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(utcNow);
        var phNowDate = philippinesNow.Date;

        bool IsReservationDateMatch(ParkFlow.Domain.Entities.ParkingReservation res)
        {
            var resDate = res.ReservationDate.Date;
            var phResDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(res.ReservationDate).Date;
            return resDate == phNowDate || phResDate == phNowDate;
        }

        var todayApprovedReservation = userReservations.FirstOrDefault(r => 
            (r.VehicleId == vehicle.Id || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
            IsReservationDateMatch(r) &&
            r.Status == ReservationStatus.Approved)
            ?? userReservations.FirstOrDefault(r =>
                IsReservationDateMatch(r) &&
                r.Status == ReservationStatus.Approved);

        var todayCompletedReservation = userReservations.FirstOrDefault(r => 
            (r.VehicleId == vehicle.Id || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
            IsReservationDateMatch(r) &&
            r.Status == ReservationStatus.Completed)
            ?? userReservations.FirstOrDefault(r =>
                IsReservationDateMatch(r) &&
                r.Status == ReservationStatus.Completed);

        var systemSettings = SystemSettingsStore.Current;
        var gracePeriodMinutes = systemSettings.IsGracePeriodEnabled ? systemSettings.GracePeriodMinutes : 0;

        if (admin == null)
        {
            if (vehicle.VerificationStatus != CorVerificationStatus.Verified)
            {
                var reasonSuffix = vehicle.VerificationStatus == CorVerificationStatus.Rejected && !string.IsNullOrWhiteSpace(vehicle.RejectionReason)
                    ? $" Reason: {vehicle.RejectionReason}"
                    : "";
                return Result<CreateParkingLogResponse>.Failure(
                    $"Entry denied: Vehicle is unverified or pending admin approval.{reasonSuffix}",
                    ErrorCode.Forbidden);
            }
        }

        if (todayApprovedReservation != null)
        {
            if (todayApprovedReservation.Type == ReservationType.Special)
            {
                maximumExitTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, new TimeSpan(23, 59, 59));
            }
            else
            {
                if (philippinesNow.TimeOfDay > todayApprovedReservation.EndTime)
                {
                    return Result<CreateParkingLogResponse>.Failure(
                        $"Entry denied: Reservation schedule for today ended at {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}.",
                        ErrorCode.Forbidden);
                }

                var earlyBuffer = systemSettings.IsEarlyParkingAllowed ? systemSettings.EarlyParkingMinutes : 0;
                var earliestResEntry = todayApprovedReservation.StartTime.Subtract(TimeSpan.FromMinutes(earlyBuffer));
                if (philippinesNow.TimeOfDay < earliestResEntry)
                {
                    return Result<CreateParkingLogResponse>.Failure(
                        $"Entry denied: Too early for reservation. Earliest allowed entry is {DateTime.Today.Add(earliestResEntry):hh:mm tt}.",
                        ErrorCode.BadRequest);
                }

                var resEndTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, todayApprovedReservation.EndTime);
                maximumExitTimeUtc = resEndTimeUtc.AddMinutes(gracePeriodMinutes);
            }
        }
        else
        {
            if (admin == null)
            {
                var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
                var userCors = corSubmissions.Where(c => c.UserAccountId == vehicle.OwnerId).ToList();

                var verifiedCor = userCors.FirstOrDefault(c =>
                    c.VerificationStatus == CorVerificationStatus.Verified);

                if (verifiedCor == null)
                {
                    if (todayCompletedReservation != null)
                    {
                        return Result<CreateParkingLogResponse>.Failure(
                            "Entry denied: This reservation pass has already been used and is now void. Re-entry is not permitted.",
                            ErrorCode.Forbidden);
                    }
                    var latestCor = userCors.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                    var docName = student != null ? "Student COR" : "Personnel ID / Registration";
                    if (latestCor?.VerificationStatus == CorVerificationStatus.Rejected)
                    {
                        var reasonSuffix = !string.IsNullOrWhiteSpace(latestCor.RejectionReason)
                            ? $" Reason: {latestCor.RejectionReason}"
                            : "";
                        return Result<CreateParkingLogResponse>.Failure(
                            $"Entry denied: {docName} was rejected.{reasonSuffix}",
                            ErrorCode.Forbidden);
                    }
                    return Result<CreateParkingLogResponse>.Failure(
                        $"Entry denied: {docName} document is unverified or pending admin approval.",
                        ErrorCode.Forbidden);
                }

                var schedules = (await _parkingScheduleRepository.GetByUserIdAsync(vehicle.OwnerId)).ToList();
                if (!schedules.Any())
                {
                    schedules = (await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id)).ToList();
                }
                var todayDayOfWeek = philippinesNow.DayOfWeek;

                var todaySchedules = schedules
                    .Where(s => s.DayOfWeek == todayDayOfWeek)
                    .OrderBy(s => s.StartTime)
                    .ToList();

                if (todaySchedules.Count == 0)
                {
                    if (todayCompletedReservation != null)
                    {
                        return Result<CreateParkingLogResponse>.Failure(
                            "Entry denied: This reservation pass has already been used and is now void. Re-entry is not permitted.",
                            ErrorCode.Forbidden);
                    }
                    return Result<CreateParkingLogResponse>.Failure(
                        $"Entry denied: No class or work schedule submitted for today ({todayDayOfWeek}).",
                        ErrorCode.Forbidden);
                }

                var validSchedule = todaySchedules.FirstOrDefault(s => _scheduleService.CanEnter(philippinesNow, s));
                if (validSchedule == null)
                {
                    if (todayCompletedReservation != null)
                    {
                        return Result<CreateParkingLogResponse>.Failure(
                            "Entry denied: This reservation pass has already been used and is now void. Re-entry is not permitted.",
                            ErrorCode.Forbidden);
                    }

                    var latestEnd = todaySchedules.Max(s => s.EndTime);
                    if (philippinesNow.TimeOfDay > latestEnd)
                    {
                        return Result<CreateParkingLogResponse>.Failure(
                            $"Entry denied: Scheduled classes for today ended at {DateTime.Today.Add(latestEnd):hh:mm tt}.",
                            ErrorCode.Forbidden);
                    }

                    var earliestStart = todaySchedules.Min(s => _scheduleService.GetEarliestAllowedEntryTime(s));
                    if (philippinesNow.TimeOfDay < earliestStart)
                    {
                        return Result<CreateParkingLogResponse>.Failure(
                            $"Entry denied: Too early for class schedule. Earliest allowed entry is {DateTime.Today.Add(earliestStart):hh:mm tt}.",
                            ErrorCode.BadRequest);
                    }

                    return Result<CreateParkingLogResponse>.Failure(
                        "Entry denied: Entry time does not align with authorized schedule.",
                        ErrorCode.BadRequest);
                }

                var scheduleEndTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, validSchedule.EndTime);
                maximumExitTimeUtc = scheduleEndTimeUtc.AddMinutes(gracePeriodMinutes);
            }
        }

        var guardProfileId = guard?.UserProfileId ?? userProfile?.Id;
        var parkingLog = _parkingService.CreateEntry(vehicle.Id, guardProfileId);
        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var guardMiddle = userProfile != null && !string.IsNullOrWhiteSpace(userProfile.MiddleName) ? $" {userProfile.MiddleName}" : "";
        var guardName = userProfile != null
            ? $"{userProfile.FirstName}{guardMiddle} {userProfile.LastName}"
            : "Campus Security";

        var response = new CreateParkingLogResponse
        {
            FirstName = ownerProfile.FirstName,
            LastName = ownerProfile.LastName,
            MiddleName = ownerProfile.MiddleName,
            Role = roleDetails.Role,
            Status = parkingLog.Status.ToString(),
            PhoneNumber = ownerProfile.UserAccount?.PhoneNumber ?? string.Empty,
            PlateNumber = vehicle.PlateNumber,
            Brand = vehicle.Brand,
            QrCodeHash = vehicle.QrCodeHash,
            VehicleType = vehicle.VehicleType.ToString(),
            EntryTime = parkingLog.EntryTime,
            EntryDate = parkingLog.EntryTime.Date,
            MaximumExitTime = maximumExitTimeUtc,
            EntryMethod = parkingLog.EntryMethod.ToString(),
            GuardName = guardName,
            IssuedBy = guardName
        };

        if (_notificationService != null && vehicle.OwnerId != Guid.Empty)
        {
            await _notificationService.CreateAndSendNotificationAsync(
                vehicle.OwnerId,
                "Parking Entry Recorded",
                $"Vehicle [{vehicle.PlateNumber}] entry pass validated by {guardName}.",
                type: "reminder",
                subtitle: $"Issued by: {guardName}",
                vehiclePlate: vehicle.PlateNumber,
                vehicleBrand: vehicle.Brand,
                actionRoute: "/(users)/(tabs)/home",
                actionText: "View Session Status",
                priority: "medium",
                issuer: guardName,
                driverName: $"{ownerProfile.FirstName} {ownerProfile.LastName}".Trim(),
                driverRole: roleDetails.Role,
                signalRData: response
            );
        }
        else
        {
            try
            {
                if (vehicle.OwnerId != Guid.Empty)
                {
                    await _signalRNotificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ParkingSessionUpdated", response);
                }
            }
            catch
            {
                // Ignore SignalR dispatch failure
            }
        }

        try
        {
            await _signalRNotificationSender.SendToAllAsync("ParkingOccupancyUpdated", new { plateNumber = vehicle.PlateNumber, action = "CheckIn" });
            await _signalRNotificationSender.SendToAllAsync("ParkingSessionUpdated", response);
        }
        catch
        {
            // Ignore SignalR dispatch failure
        }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.SessionPrefix, cancellationToken);
        }

        return Result<CreateParkingLogResponse>.Success(response, "Entry Confirmed");
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
