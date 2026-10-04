using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Services;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;

public class VerifyStudentScanHandler : IRequestHandler<VerifyStudentScanQuery, Result<VerifyStudentScanResponse>>
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ICorSubmissionRepository _corSubmissionRepository;
    private readonly IParkingScheduleRepository _parkingScheduleRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IViolationRepository _violationRepository;
    private readonly IScheduleService _scheduleService;
    private readonly IParkingReservationRepository? _reservationRepository;

    public VerifyStudentScanHandler(
        IStudentRepository studentRepository,
        IUserProfileRepository userProfileRepository,
        ICorSubmissionRepository corSubmissionRepository,
        IParkingScheduleRepository parkingScheduleRepository,
        IVehicleRepository vehicleRepository,
        IParkingLogRepository parkingLogRepository,
        IViolationRepository violationRepository,
        IScheduleService scheduleService,
        IParkingReservationRepository? reservationRepository = null)
    {
        _studentRepository = studentRepository;
        _userProfileRepository = userProfileRepository;
        _corSubmissionRepository = corSubmissionRepository;
        _parkingScheduleRepository = parkingScheduleRepository;
        _vehicleRepository = vehicleRepository;
        _parkingLogRepository = parkingLogRepository;
        _violationRepository = violationRepository;
        _scheduleService = scheduleService;
        _reservationRepository = reservationRepository;
    }

    public async Task<Result<VerifyStudentScanResponse>> Handle(VerifyStudentScanQuery request, CancellationToken cancellationToken)
    {
        string? studentNumber = request.StudentNumber?.Trim();
        string? scannedName = request.FullName?.Trim();
        string? scannedProgram = request.Program?.Trim();

        if (!string.IsNullOrWhiteSpace(request.QrContent))
        {
            var raw = request.QrContent.Trim();
            var delimiters = new[] { ',', '\n', '\r', ';', '|' };
            if (delimiters.Any(d => raw.Contains(d)))
            {
                var parts = raw.Split(delimiters, StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToArray();

                if (parts.Length >= 3)
                {
                    studentNumber = CleanStudentNumber(parts[0]);
                    scannedName = CleanLabel(parts[1]);
                    scannedProgram = CleanLabel(string.Join(", ", parts.Skip(2)));
                }
                else if (parts.Length == 2)
                {
                    studentNumber = CleanStudentNumber(parts[0]);
                    scannedName = CleanLabel(parts[1]);
                }
                else if (parts.Length == 1)
                {
                    studentNumber = CleanStudentNumber(parts[0]);
                }
            }
            else if (string.IsNullOrWhiteSpace(studentNumber))
            {
                studentNumber = CleanStudentNumber(raw);
            }
        }

        if (string.IsNullOrWhiteSpace(studentNumber))
        {
            return Result<VerifyStudentScanResponse>.Failure("Could not extract a valid student number from the scanned QR code.", ErrorCode.BadRequest);
        }

        // Search in database by StudentNumber (primary & unique identifier)
        var student = await _studentRepository.GetByStudentNumberAsync(studentNumber);

        if (student == null)
        {
            var notFoundResponse = new VerifyStudentScanResponse
            {
                IsValid = false,
                EntryStatus = "NotFound",
                StatusMessage = $"Student number '{studentNumber}' is not registered in ParkFlow.",
                StudentNumber = studentNumber,
                FullName = !string.IsNullOrWhiteSpace(scannedName) ? scannedName : "Unregistered Student",
                Program = !string.IsNullOrWhiteSpace(scannedProgram) ? scannedProgram : "N/A",
                TodaySchedule = "No record found",
                AllowedEntryWindow = "N/A",
                CorStatus = "Not Registered",
                HasRegisteredVehicle = false
            };

            return Result<VerifyStudentScanResponse>.Success(notFoundResponse, notFoundResponse.StatusMessage);
        }

        // Student found - resolve Profile
        var profile = student.UserProfile ?? await _userProfileRepository.GetByIdAsync(student.UserProfileId);
        var studentFullName = profile != null
            ? $"{profile.FirstName} {profile.LastName}".Trim()
            : (!string.IsNullOrWhiteSpace(scannedName) ? scannedName : "Student");

        var studentProgram = !string.IsNullOrWhiteSpace(student.Course)
            ? student.Course
            : (!string.IsNullOrWhiteSpace(scannedProgram) ? scannedProgram : "N/A");

        var userAccountId = profile != null ? profile.UserAccountId : Guid.Empty;

        // Check registered vehicle
        Vehicle? primaryVehicle = null;
        bool hasRegisteredVehicle = false;
        bool hasActiveViolation = false;
        string? violationNotice = null;
        bool isCurrentlyParked = false;

        if (userAccountId != Guid.Empty)
        {
            var userVehicles = await _vehicleRepository.GetByOwnerIdAsync(userAccountId);
            var vehicleList = userVehicles.ToList();
            primaryVehicle = vehicleList.FirstOrDefault(v => v.IsPrimary) ?? vehicleList.FirstOrDefault();

            if (primaryVehicle != null)
            {
                hasRegisteredVehicle = true;
                var activeViolationCount = await _violationRepository.GetActiveViolationCountAsync(primaryVehicle.Id, userAccountId);
                hasActiveViolation = activeViolationCount >= 3;
                if (activeViolationCount >= 3)
                {
                    violationNotice = $"User has {activeViolationCount} unpaid violations (maximum limit is 3). Entry suspended.";
                }
                else if (activeViolationCount > 0)
                {
                    violationNotice = $"User has {activeViolationCount} unpaid violation(s). Entry permitted (maximum 3 allowed before suspension).";
                }

                var activeLog = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(primaryVehicle.Id);
                if (activeLog != null)
                {
                    isCurrentlyParked = true;
                }
            }
        }

        // Check schedule and COR
        bool isValid = false;
        string entryStatus;
        string statusMessage;
        string todaySchedule = "No schedule submitted";
        string? allowedEntryWindow = null;
        string corStatus = "NotSubmitted";

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(DateTime.UtcNow);
        var todayDayOfWeek = philippinesNow.DayOfWeek;

        if (userAccountId == Guid.Empty)
        {
            entryStatus = "CorNotVerified";
            statusMessage = "Student account record is incomplete.";
        }
        else
        {
            var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
            var userCors = corSubmissions.Where(c => c.UserAccountId == userAccountId).ToList();

            var verifiedCor = userCors.FirstOrDefault(c => c.VerificationStatus == CorVerificationStatus.Verified);

            if (userCors.Count == 0)
            {
                entryStatus = "CorNotVerified";
                corStatus = "NotSubmitted";
                statusMessage = "Entry denied: Student has not submitted a Certificate of Registration (COR).";
                todaySchedule = "No schedule submitted";
                allowedEntryWindow = "N/A";
            }
            else if (verifiedCor == null)
            {
                var latestCor = userCors.OrderByDescending(c => c.CreatedAt).FirstOrDefault();
                corStatus = latestCor?.VerificationStatus.ToString() ?? "Pending";
                entryStatus = "CorNotVerified";

                if (latestCor?.VerificationStatus == CorVerificationStatus.Rejected)
                {
                    statusMessage = $"Entry denied: Student COR was rejected. {(!string.IsNullOrWhiteSpace(latestCor.RejectionReason) ? $"Reason: {latestCor.RejectionReason}" : "Please submit an updated COR.")}";
                }
                else
                {
                    statusMessage = "Entry denied: Student COR document is unverified or pending admin approval.";
                }

                todaySchedule = "Pending verification";
                allowedEntryWindow = "N/A";
            }
            else
            {
                corStatus = "Verified";
                var schedules = (await _parkingScheduleRepository.GetByUserIdAsync(userAccountId)).ToList();
                if (!schedules.Any())
                {
                    schedules = (await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id)).ToList();
                }
                var todaySchedules = schedules
                    .Where(s => s.DayOfWeek == todayDayOfWeek)
                    .OrderBy(s => s.StartTime)
                    .ToList();

                ParkingReservation? todayApprovedReservation = null;
                ParkingReservation? todayCompletedReservation = null;

                if (_reservationRepository != null)
                {
                    var userReservations = (await _reservationRepository.GetByUserIdAsync(userAccountId)).ToList();
                    var phNowDate = philippinesNow.Date;
                    bool IsReservationDateMatch(ParkingReservation res)
                    {
                        var resDate = res.ReservationDate.Date;
                        var phResDate = ParkingTimeHelper.ConvertUtcToPhilippinesTime(res.ReservationDate).Date;
                        return resDate == phNowDate || phResDate == phNowDate;
                    }

                    todayApprovedReservation = userReservations.FirstOrDefault(r =>
                        (primaryVehicle == null || r.VehicleId == primaryVehicle.Id || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
                        IsReservationDateMatch(r) &&
                        r.Status == ReservationStatus.Approved)
                        ?? userReservations.FirstOrDefault(r =>
                            IsReservationDateMatch(r) &&
                            r.Status == ReservationStatus.Approved);

                    todayCompletedReservation = userReservations.FirstOrDefault(r =>
                        (primaryVehicle == null || r.VehicleId == primaryVehicle.Id || r.VehicleId == null || r.VehicleId == Guid.Empty) &&
                        IsReservationDateMatch(r) &&
                        r.Status == ReservationStatus.Completed)
                        ?? userReservations.FirstOrDefault(r =>
                            IsReservationDateMatch(r) &&
                            r.Status == ReservationStatus.Completed);
                }

                if (todaySchedules.Count == 0)
                {
                    if (todayApprovedReservation != null)
                    {
                        var settings = SystemSettingsStore.Current;
                        var earlyBuffer = settings.IsEarlyParkingAllowed ? settings.EarlyParkingMinutes : 0;
                        var earliestAllowed = todayApprovedReservation.StartTime.Subtract(TimeSpan.FromMinutes(earlyBuffer));

                        if (todayApprovedReservation.Type != ReservationType.Special && philippinesNow.TimeOfDay > todayApprovedReservation.EndTime)
                        {
                            entryStatus = "OutOfSchedule";
                            statusMessage = $"Entry denied: Reservation schedule for today ended at {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}.";
                            todaySchedule = $"Reservation Pass: {DateTime.Today.Add(todayApprovedReservation.StartTime):hh:mm tt} – {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt} (Ended)";
                            allowedEntryWindow = "N/A";
                        }
                        else if (todayApprovedReservation.Type != ReservationType.Special && philippinesNow.TimeOfDay < earliestAllowed)
                        {
                            entryStatus = "OutOfSchedule";
                            statusMessage = $"Entry denied: Too early for reservation. Earliest allowed entry is {DateTime.Today.Add(earliestAllowed):hh:mm tt}.";
                            todaySchedule = $"Reservation Pass: {DateTime.Today.Add(todayApprovedReservation.StartTime):hh:mm tt} – {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}";
                            allowedEntryWindow = $"{DateTime.Today.Add(earliestAllowed):hh:mm tt} – {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}";
                        }
                        else
                        {
                            isValid = true;
                            entryStatus = "Approved";
                            statusMessage = "Authorized for campus entry today (via reservation pass).";
                            todaySchedule = $"Reservation Pass: {DateTime.Today.Add(todayApprovedReservation.StartTime):hh:mm tt} – {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}";
                            allowedEntryWindow = $"{DateTime.Today.Add(earliestAllowed):hh:mm tt} – {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}";
                        }
                    }
                    else if (todayCompletedReservation != null)
                    {
                        entryStatus = "NoScheduleToday";
                        statusMessage = "Entry denied: This reservation pass has already been used and is now void. Re-entry is not permitted without an active class schedule for today.";
                        todaySchedule = $"No classes scheduled on {todayDayOfWeek}";
                        allowedEntryWindow = "N/A";
                    }
                    else
                    {
                        entryStatus = "NoScheduleToday";
                        statusMessage = $"Entry denied: No classes scheduled for today ({todayDayOfWeek}).";
                        todaySchedule = $"No classes scheduled on {todayDayOfWeek}";
                        allowedEntryWindow = "N/A";
                    }
                }
                else
                {
                    todaySchedule = string.Join(", ", todaySchedules.Select(s =>
                        $"{DateTime.Today.Add(s.StartTime):hh:mm tt} – {DateTime.Today.Add(s.EndTime):hh:mm tt}"));

                    var matchingSchedule = todaySchedules.FirstOrDefault(s => _scheduleService.CanEnter(philippinesNow, s));

                    if (matchingSchedule != null)
                    {
                        isValid = true;
                        entryStatus = "Approved";
                        var earliest = _scheduleService.GetEarliestAllowedEntryTime(matchingSchedule);
                        allowedEntryWindow = $"{DateTime.Today.Add(earliest):hh:mm tt} – {DateTime.Today.Add(matchingSchedule.EndTime):hh:mm tt} (with 30m grace)";
                        statusMessage = "Authorized for campus entry today.";
                    }
                    else if (todayApprovedReservation != null)
                    {
                        var settings = SystemSettingsStore.Current;
                        var earlyBuffer = settings.IsEarlyParkingAllowed ? settings.EarlyParkingMinutes : 0;
                        var earliestAllowed = todayApprovedReservation.StartTime.Subtract(TimeSpan.FromMinutes(earlyBuffer));

                        if (todayApprovedReservation.Type == ReservationType.Special || (philippinesNow.TimeOfDay >= earliestAllowed && philippinesNow.TimeOfDay <= todayApprovedReservation.EndTime))
                        {
                            isValid = true;
                            entryStatus = "Approved";
                            statusMessage = "Authorized for campus entry today (via reservation pass).";
                            allowedEntryWindow = $"{DateTime.Today.Add(earliestAllowed):hh:mm tt} – {DateTime.Today.Add(todayApprovedReservation.EndTime):hh:mm tt}";
                        }
                        else
                        {
                            entryStatus = "OutOfSchedule";
                            var earliestFirst = _scheduleService.GetEarliestAllowedEntryTime(todaySchedules.First());
                            var latestEnd = todaySchedules.Last().EndTime;

                            if (philippinesNow.TimeOfDay < earliestFirst)
                            {
                                statusMessage = $"Entry denied: Too early for class. Earliest allowed entry is {DateTime.Today.Add(earliestFirst):hh:mm tt}.";
                            }
                            else if (philippinesNow.TimeOfDay > latestEnd)
                            {
                                statusMessage = $"Entry denied: Scheduled classes ended at {DateTime.Today.Add(latestEnd):hh:mm tt}.";
                            }
                            else
                            {
                                statusMessage = "Entry denied: Current time falls outside authorized class schedule windows.";
                            }

                            allowedEntryWindow = string.Join(", ", todaySchedules.Select(s =>
                                $"{DateTime.Today.Add(_scheduleService.GetEarliestAllowedEntryTime(s)):hh:mm tt} – {DateTime.Today.Add(s.EndTime):hh:mm tt}"));
                        }
                    }
                    else
                    {
                        entryStatus = "OutOfSchedule";
                        var earliestFirst = _scheduleService.GetEarliestAllowedEntryTime(todaySchedules.First());
                        var latestEnd = todaySchedules.Last().EndTime;

                        if (todayCompletedReservation != null)
                        {
                            statusMessage = "Entry denied: Reservation pass has already concluded and current time falls outside authorized class schedule windows.";
                        }
                        else if (philippinesNow.TimeOfDay < earliestFirst)
                        {
                            statusMessage = $"Entry denied: Too early for class. Earliest allowed entry is {DateTime.Today.Add(earliestFirst):hh:mm tt}.";
                        }
                        else if (philippinesNow.TimeOfDay > latestEnd)
                        {
                            statusMessage = $"Entry denied: Scheduled classes ended at {DateTime.Today.Add(latestEnd):hh:mm tt}.";
                        }
                        else
                        {
                            statusMessage = "Entry denied: Current time falls outside authorized class schedule windows.";
                        }

                        allowedEntryWindow = string.Join(", ", todaySchedules.Select(s =>
                            $"{DateTime.Today.Add(_scheduleService.GetEarliestAllowedEntryTime(s)):hh:mm tt} – {DateTime.Today.Add(s.EndTime):hh:mm tt}"));
                    }
                }
            }
        }

        // If the student's registered vehicle is already parked inside the facility,
        // this scan is for EXIT, not entry. Do not deny exit based on class schedule!
        if (isCurrentlyParked && primaryVehicle != null)
        {
            isValid = true;
            entryStatus = "CurrentlyParked";
            statusMessage = $"Vehicle ({primaryVehicle.PlateNumber}) is currently parked inside. Ready to exit.";
        }
        else if (hasActiveViolation)
        {
            isValid = false;
            entryStatus = "HasViolation";
            statusMessage = "Entry denied: User has reached 3 or more unpaid violations. Please settle pending charges before parking.";
        }
        else if (isValid && primaryVehicle != null && primaryVehicle.VerificationStatus != CorVerificationStatus.Verified)
        {
            isValid = false;
            entryStatus = "VehicleNotVerified";
            var reasonSuffix = primaryVehicle.VerificationStatus == CorVerificationStatus.Rejected && !string.IsNullOrWhiteSpace(primaryVehicle.RejectionReason)
                ? $" Reason: {primaryVehicle.RejectionReason}"
                : "";
            statusMessage = $"Entry denied: Vehicle is unverified or pending admin approval.{reasonSuffix}";
        }

        var response = new VerifyStudentScanResponse
        {
            IsValid = isValid,
            EntryStatus = entryStatus,
            StatusMessage = statusMessage,
            StudentNumber = student.StudentNumber,
            FullName = studentFullName,
            Program = studentProgram,
            Section = student.Section,
            YearLevel = student.YearLevel,
            ProfilePictureUrl = profile?.ProfilePictureUrl,
            TodaySchedule = todaySchedule,
            AllowedEntryWindow = allowedEntryWindow,
            CorStatus = corStatus,
            HasRegisteredVehicle = hasRegisteredVehicle,
            VehicleId = primaryVehicle?.Id,
            PlateNumber = primaryVehicle?.PlateNumber,
            VehicleBrand = primaryVehicle?.Brand,
            VehicleType = primaryVehicle?.VehicleType.ToString(),
            VehicleQrCodeHash = primaryVehicle?.QrCodeHash,
            IsCurrentlyParked = isCurrentlyParked,
            HasActiveViolation = hasActiveViolation,
            ViolationNotice = violationNotice
        };

        return Result<VerifyStudentScanResponse>.Success(response, statusMessage);
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

    private static string CleanLabel(string token, string? fieldType = null)
    {
        if (string.IsNullOrWhiteSpace(token)) return string.Empty;
        var cleaned = token.Trim();
        var colonIdx = cleaned.IndexOf(':');
        if (colonIdx >= 0 && colonIdx < cleaned.Length - 1)
        {
            cleaned = cleaned.Substring(colonIdx + 1).Trim();
        }
        return cleaned;
    }
}
