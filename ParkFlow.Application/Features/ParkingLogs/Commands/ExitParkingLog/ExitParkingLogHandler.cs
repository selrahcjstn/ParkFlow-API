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
        IEmailService? emailService = null)
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

        if (vehicle == null)
            return Result<ExitParkingLogResponse>.Failure("Invalid QR code. Vehicle not found.", ErrorCode.NotFound);

        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.UserId);

        if (userProfile == null)
            return Result<ExitParkingLogResponse>.Failure("User profile not found.", ErrorCode.NotFound);

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);

        if (guard == null)
            return Result<ExitParkingLogResponse>.Failure("Guard not found.", ErrorCode.NotFound);

        var active = await _parkingLogRepository.GetActiveParkingLogByVehicleIdAsync(vehicle.Id);

        if (active == null)
            return Result<ExitParkingLogResponse>.Failure("No active parking log found for this vehicle.", ErrorCode.NotFound);

        var statusBeforeExit = active.Status;
        var exitTime = DateTime.UtcNow;

        _parkingService.MarkExit(active);
        await _parkingLogRepository.UpdateParkingLogAsync(active);

        var corSubmissions = await _corSubmissionRepository.ListCorSubmissionsAsync();
        var verifiedCor = corSubmissions.FirstOrDefault(c => c.UserAccountId == vehicle.OwnerId && c.VerificationStatus == CorVerificationStatus.Verified);

        var endTime = exitTime;
        DateTime? maximumExitTime = null;
        double overstayTime = 0;
        decimal penaltyFee = 0m;
        bool isViolation = false;
        Guid? violationId = null;
        string? violationType = null;
        string? settlementStatus = null;
        string? referenceNumber = null;

        var philippinesNow = ParkingTimeHelper.ConvertUtcToPhilippinesTime(exitTime);

        if (active.EntryMethod != EntryMethod.Manual && verifiedCor != null)
        {
            var schedules = await _parkingScheduleRepository.GetBySubmissionIdAsync(verifiedCor.Id);
            var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == philippinesNow.DayOfWeek);

            if (todaySchedule != null)
            {
                endTime = ParkingTimeHelper.BuildPhilippinesScheduleUtcDateTime(philippinesNow, todaySchedule.EndTime);
                maximumExitTime = endTime.AddMinutes(30);

                if (_violationService.IsOverstay(philippinesNow, todaySchedule.EndTime))
                {
                    var overstayDuration = _violationService.GetOverstayDuration(philippinesNow, todaySchedule.EndTime);
                    overstayTime = overstayDuration.TotalHours;
                    penaltyFee = _violationService.CalculatePenalty(overstayDuration);

                    if (penaltyFee > 0m)
                    {
                        var recordedExitTime = active.ExitTime ?? exitTime;
                        var violation = new Violation(
                            active.Id,
                            penaltyFee);
                        await _violationRepository.AddAsync(violation);
                        isViolation = true;
                        violationId = violation.Id;
                        violationType = violation.ViolationType.ToString();
                        settlementStatus = violation.SettlementStatus.ToString();
                        referenceNumber = violation.ReferenceNumber;
                    }
                }
            }
        }

        var userReservations = await _reservationRepository.GetByUserIdAsync(vehicle.OwnerId);
        var specialRes = userReservations.FirstOrDefault(r => 
            (r.VehicleId == vehicle.Id || r.VehicleId == null) &&
            r.ReservationDate.Date == philippinesNow.Date &&
            r.Status == ReservationStatus.Approved &&
            r.Type == ReservationType.Special);

        if (specialRes != null)
        {
            penaltyFee = 0m;
        }

        if (!isViolation && specialRes == null)
        {
            var existingViolation = await _violationRepository.GetByLogIdAsync(active.Id);
            if (existingViolation != null)
            {
                isViolation = true;
                violationId = existingViolation.Id;
                violationType = existingViolation.ViolationType.ToString();
                settlementStatus = existingViolation.SettlementStatus.ToString();
                referenceNumber = existingViolation.ReferenceNumber ?? $"VIO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
            }
        }


        var actualExitTime = active.ExitTime ?? exitTime;

        var ownerProfile = await _userProfileRepository.GetByUserIdAsync(vehicle.OwnerId);

        if (ownerProfile == null)
            return Result<ExitParkingLogResponse>.Failure("Owner profile not found.", ErrorCode.NotFound);

        var student = await _studentRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var personnel = await _personnelRepository.GetByUserProfileIdAsync(ownerProfile.Id);
        var admin = await _adminRepository.GetByUserProfileIdAsync(ownerProfile.Id);

        var roleDetails = _parkingLogRoleService.GetRoleDetails(ownerProfile, student, personnel, admin);

        var response = new ExitParkingLogResponse
        {
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
            PenaltyFee = penaltyFee,
            ReferenceNumber = referenceNumber
        };

        if (vehicle.OwnerId != Guid.Empty)
        {
            var guardMiddle = string.IsNullOrWhiteSpace(userProfile.MiddleName) ? "" : $" {userProfile.MiddleName}";
            var guardName = $"{userProfile.FirstName}{guardMiddle} {userProfile.LastName}";

            var notificationDto = new HasViolationNotificationDto
            {
                ReferenceNumber = referenceNumber ?? response.ReferenceNumber ?? $"EXIT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                RefNumber = referenceNumber ?? response.ReferenceNumber ?? $"EXIT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                IssuedDate = exitTime,
                IssuedTime = exitTime,
                IssuedBy = guardName,
                OverstayHours = overstayTime,
                PlateNumber = vehicle.PlateNumber,
                Amount = penaltyFee,
                ViolationType = violationType ?? (isViolation ? "Overstay" : "Normal Exit"),
                IsViolation = isViolation
            };

            try
            {
                await _notificationSender.SendEventNotificationAsync(vehicle.OwnerId.ToString(), notificationDto);
                await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ParkingSessionUpdated", response);
                await _notificationSender.SendToAllAsync("ParkingSessionUpdated", response);

                if (isViolation)
                {
                    await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "ReceiveViolation", notificationDto);
                }
            }
            catch
            {
                // Ignore SignalR dispatch failure
            }

            // Gmail Email Notification on Exit ONLY
            if (_userAccountRepository != null && _emailService != null)
            {
                try
                {
                    var ownerAccount = await _userAccountRepository.GetByIdAsync(vehicle.OwnerId);
                    if (ownerAccount != null && !string.IsNullOrWhiteSpace(ownerAccount.PrimaryEmail))
                    {
                        var exitPhTime = ParkingTimeHelper.ConvertUtcToPhilippinesTime(actualExitTime);
                        var entryPhTime = ParkingTimeHelper.ConvertUtcToPhilippinesTime(active.EntryTime);
                        var subject = $"ParkFlow Exit Pass Notice - Vehicle [{vehicle.PlateNumber}]";

                        var penaltyText = penaltyFee > 0m 
                            ? $"<span style=\"color: #dc2626; font-weight: bold;\">₱{penaltyFee:0.00} (Overstay Citation)</span>" 
                            : "<span style=\"color: #16a34a; font-weight: bold;\">₱0.00 (Cleared)</span>";

                        var bodyHtml = $@"
                            <div style=""font-family: Arial, sans-serif; max-width: 580px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 14px; background-color: #ffffff;"">
                              <div style=""background: linear-gradient(135deg, #d22730 0%, #991b1b 100%); padding: 18px 24px; border-radius: 10px 10px 0 0; text-align: center;"">
                                <h2 style=""color: #ffffff; margin: 0; font-size: 20px; font-weight: 800; letter-spacing: 0.5px;"">ParkFlow Parking Exit Receipt</h2>
                                <p style=""color: #fecdd3; margin: 4px 0 0; font-size: 12px; font-weight: 600;"">Official Gate Exit Notice</p>
                              </div>
                              <div style=""padding: 24px;"">
                                <p style=""font-size: 15px; color: #1e293b; margin-top: 0;"">Hello <strong>{ownerProfile.FirstName} {ownerProfile.LastName}</strong>,</p>
                                <p style=""font-size: 14px; color: #475569; line-height: 1.5;"">
                                  Your registered vehicle <strong>{vehicle.PlateNumber} ({vehicle.Brand})</strong> has successfully logged an exit from the campus parking facility.
                                </p>

                                <div style=""background-color: #f8fafc; border: 1px solid #e2e8f0; border-left: 4px solid #d22730; padding: 16px; margin: 20px 0; border-radius: 8px;"">
                                  <table style=""width: 100%; border-collapse: collapse; font-size: 13.5px;"">
                                    <tr>
                                      <td style=""padding: 6px 0; color: #64748b;"">Plate Number:</td>
                                      <td style=""padding: 6px 0; font-weight: bold; color: #0f172a; text-align: right;"">{vehicle.PlateNumber}</td>
                                    </tr>
                                    <tr>
                                      <td style=""padding: 6px 0; color: #64748b;"">Entry Time:</td>
                                      <td style=""padding: 6px 0; color: #0f172a; text-align: right;"">{entryPhTime:MMMM dd, yyyy · hh:mm tt}</td>
                                    </tr>
                                    <tr>
                                      <td style=""padding: 6px 0; color: #64748b;"">Exit Time:</td>
                                      <td style=""padding: 6px 0; color: #0f172a; text-align: right;"">{exitPhTime:MMMM dd, yyyy · hh:mm tt}</td>
                                    </tr>
                                    <tr>
                                      <td style=""padding: 6px 0; color: #64748b;"">Overstay Penalty Fee:</td>
                                      <td style=""padding: 6px 0; text-align: right;"">{penaltyText}</td>
                                    </tr>
                                  </table>
                                </div>

                                <p style=""font-size: 12.5px; color: #64748b; margin-bottom: 0; line-height: 1.5; text-align: center;"">
                                  Thank you for utilizing ParkFlow Campus Smart Parking Services.<br/>
                                  <span style=""font-size: 11px; color: #94a3b8;"">This is an automated system notification. Please do not reply directly to this email.</span>
                                </p>
                              </div>
                            </div>";

                        await _emailService.SendEmailAsync(ownerAccount.PrimaryEmail, subject, bodyHtml);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ExitParkingLogHandler] Email notice failed: {ex.Message}");
                }
            }
        }

        return Result<ExitParkingLogResponse>.Success(response, "Exit Confirmed");
    }
}
