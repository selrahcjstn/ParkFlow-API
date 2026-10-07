using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;

public class CreateManualParkingLogHandler : IRequestHandler<CreateManualParkingLogCommand, Result<CreateParkingLogResponse>>
{
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IUserAccountRepository _userAccountRepository;
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
    private readonly INotificationService? _notificationService;
    private readonly IParkingReservationRepository? _reservationRepository;
    private readonly ICacheService? _cacheService;

    public CreateManualParkingLogHandler(
        IParkingLogRepository parkingLogRepository,
        IVehicleRepository vehicleRepository,
        IUserProfileRepository userProfileRepository,
        IUserAccountRepository userAccountRepository,
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
        INotificationService? notificationService = null,
        IParkingReservationRepository? reservationRepository = null,
        ICacheService? cacheService = null)
    {
        _parkingLogRepository = parkingLogRepository;
        _vehicleRepository = vehicleRepository;
        _userProfileRepository = userProfileRepository;
        _userAccountRepository = userAccountRepository;
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
        _notificationService = notificationService;
        _reservationRepository = reservationRepository;
        _cacheService = cacheService;
    }

    public async Task<Result<CreateParkingLogResponse>> Handle(CreateManualParkingLogCommand request, CancellationToken cancellationToken)
    {
        // 1. Find vehicle by plate number
        var vehicle = await _vehicleRepository.GetByPlateNumberAsync(request.PlateNumber.Trim().ToUpperInvariant());

        if (vehicle == null)
            return Result<CreateParkingLogResponse>.Failure(
                "This plate is not registered. Use visitor entry to record it.", ErrorCode.NotFound);

        var chargeUnscheduledFee = false;

        // 2. Fetch owner profile
        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);
        if (ownerProfile == null)
            return Result<CreateParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        // 3. Check duplicate active parking session
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

        // 3b. Check active violations
        var activeViolationCount = await _violationRepository.GetActiveViolationCountAsync(vehicle.Id, vehicle.OwnerId);
        if (activeViolationCount > 0)
        {
            return Result<CreateParkingLogResponse>.Failure(
                "Entry denied. Please settle all unpaid violations before entering campus.",
                ErrorCode.Forbidden);
        }

        // 4. Check guard or admin exists
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);
        Guard? guard = null;
        Admin? callerAdmin = null;

        if (userProfile != null)
        {
            guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
            callerAdmin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
        }

        if (guard == null && callerAdmin == null)
        {
            return Result<CreateParkingLogResponse>.Failure("Only a guard or admin can record entry.", ErrorCode.Forbidden);
        }

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var isGuest = (ownerProfile.FirstName == "Guest" && ownerProfile.LastName == "User")
            || (ownerProfile.UserAccount != null && ownerProfile.UserAccount.PhoneNumber == "+00000000000");

        var isStudentOrPersonnel = (student != null || personnel != null) || (admin == null && !isGuest);

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

        if (admin == null && !isGuest)
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

        if (PersonnelParkingPolicy.AppliesTo(personnel))
        {
            var employeeSubmissions = await _corSubmissionRepository.GetByUserIdsAsync([vehicle.OwnerId]);
            var verifiedEmployeeId = employeeSubmissions.Any(c =>
                c.VerificationStatus == CorVerificationStatus.Verified &&
                !string.IsNullOrWhiteSpace(c.CorDocumentUrl) &&
                !string.Equals(c.CorDocumentUrl, "pending", StringComparison.OrdinalIgnoreCase));
            if (!verifiedEmployeeId)
                return Result<CreateParkingLogResponse>.Failure(
                    "Entry denied: Employee ID is missing or waiting for approval.", ErrorCode.Forbidden);

            maximumExitTimeUtc = PersonnelParkingPolicy.GetDeadlineUtc(utcNow, systemSettings);
        }
        else if (todayApprovedReservation != null)
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
        else if (admin == null && !isGuest)
        {
            var corSubmissions = await _corSubmissionRepository.GetByUserIdsAsync([vehicle.OwnerId]);
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
                if (!request.AcceptUnscheduledFee)
                    return Result<CreateParkingLogResponse>.Failure(
                        new CreateParkingLogResponse { FeeOptionAvailable = true, EntryFee = 20m },
                        "No class or work schedule for today. You may continue with a ₱20 parking fee, payable on exit.",
                        ErrorCode.Forbidden);

                chargeUnscheduledFee = true;
            }

            if (!chargeUnscheduledFee)
            {
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

        if (maximumExitTimeUtc == null)
        {
            maximumExitTimeUtc = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, new TimeSpan(23, 59, 59));
        }

        // 5. Create Entry with manual method
        var parkingLog = _parkingService.CreateEntry(vehicle.Id, guard?.UserProfileId, chargeUnscheduledFee || isGuest ? EntryMethod.Manual : EntryMethod.ManualScheduled);
        await _parkingLogRepository.AddParkingLogAsync(parkingLog);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var guardMiddle = userProfile != null && !string.IsNullOrWhiteSpace(userProfile.MiddleName) ? $" {userProfile.MiddleName}" : "";
        var guardName = userProfile != null
            ? $"{userProfile.FirstName}{guardMiddle} {userProfile.LastName}"
            : "Campus Administrator";

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
            EntryFee = chargeUnscheduledFee || isGuest ? 20m : 0m,
            IssuedBy = guardName
        };

        if (_notificationService != null && vehicle.OwnerId != Guid.Empty)
        {
            await _notificationService.CreateAndSendNotificationAsync(
                vehicle.OwnerId,
                "Manual Parking Entry Recorded",
                $"Vehicle [{vehicle.PlateNumber}] manual entry pass validated by {guardName}.",
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
}
