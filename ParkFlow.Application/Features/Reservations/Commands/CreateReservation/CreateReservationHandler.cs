using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Result<ParkingReservationDto>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IValidator<CreateReservationCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IEmailService _emailService;
    private readonly ICorSubmissionRepository? _corSubmissionRepository;
    private readonly IViolationRepository? _violationRepository;

    public CreateReservationHandler(
        IParkingReservationRepository reservationRepository,
        IUserAccountRepository userRepository,
        IVehicleRepository vehicleRepository,
        IValidator<CreateReservationCommand> validator,
        ISignalRNotificationSender notificationSender,
        IEmailService emailService,
        ICorSubmissionRepository? corSubmissionRepository = null,
        IViolationRepository? violationRepository = null)
    {
        _reservationRepository = reservationRepository;
        _userRepository = userRepository;
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _notificationSender = notificationSender;
        _emailService = emailService;
        _corSubmissionRepository = corSubmissionRepository;
        _violationRepository = violationRepository;
    }

    public async Task<Result<ParkingReservationDto>> Handle(CreateReservationCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ParkingReservationDto>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<ParkingReservationDto>.Failure("User not found.", ErrorCode.NotFound);

        if (_violationRepository != null && (await _violationRepository.GetActiveViolationCountByUserIdAsync(user.Id)) >= 3)
        {
            var vCount = await _violationRepository.GetActiveViolationCountByUserIdAsync(user.Id);
            return Result<ParkingReservationDto>.Failure(
                $"You have {vCount} active/unpaid violations (maximum limit is 3). Please settle pending charges before creating a reservation.",
                ErrorCode.Forbidden);
        }

        var isAccountActive = user.Status == AccountStatus.Active;

        // 1. Check if user is an Admin or Guard
        var isAdmin = user.UserProfile?.Admin != null;
        var isGuard = user.UserProfile?.Guard != null;

        // 2. Check COR verification status
        CorSubmission? latestCor = null;
        if (_corSubmissionRepository != null)
        {
            latestCor = await _corSubmissionRepository.GetLatestByUserIdAsync(user.Id);
            if (latestCor != null && latestCor.VerificationStatus == CorVerificationStatus.Verified)
            {
                isAccountActive = true;
                if (user.Status != AccountStatus.Active)
                {
                    user.Verify();
                    await _userRepository.UpdateAsync(user);
                }
            }
            else if (latestCor != null && latestCor.VerificationStatus != CorVerificationStatus.Verified)
            {
                isAccountActive = false;
            }
        }

        // 3. Check Vehicle verification status
        var userVehicles = (await _vehicleRepository.GetByOwnerIdAsync(user.Id)).ToList();
        if (userVehicles.Any(v => v.VerificationStatus == CorVerificationStatus.Verified))
        {
            // If user has a verified vehicle and COR is not explicitly rejected, user is verified
            if (latestCor == null || latestCor.VerificationStatus == CorVerificationStatus.Verified)
            {
                isAccountActive = true;
                if (user.Status != AccountStatus.Active)
                {
                    user.Verify();
                    await _userRepository.UpdateAsync(user);
                }
            }
        }

        if (isAdmin || isGuard)
        {
            isAccountActive = true;
        }

        if (!isAccountActive)
        {
            return Result<ParkingReservationDto>.Failure(
                "Your account must be verified and approved by an administrator before you can create parking reservations.",
                ErrorCode.Forbidden);
        }

        // Enforce restriction: 1 parking reservation per day per user across all platforms (Mobile & Web)
        var userReservations = (await _reservationRepository.GetByUserIdAsync(user.Id)).ToList();
        var hasReservationOnDate = userReservations.Any(r =>
            r.ReservationDate.Year == request.ReservationDate.Year &&
            r.ReservationDate.Month == request.ReservationDate.Month &&
            r.ReservationDate.Day == request.ReservationDate.Day &&
            r.Status != ReservationStatus.Cancelled &&
            r.Status != ReservationStatus.Rejected);

        if (hasReservationOnDate)
        {
            return Result<ParkingReservationDto>.Failure(
                $"You already have an active or pending reservation for {request.ReservationDate:MMMM dd, yyyy}. Only one reservation per day is allowed.",
                ErrorCode.Conflict);
        }

        // Find vehicle to bind (user's primary vehicle if vehicleId not specified)
        Guid? assignedVehicleId = request.VehicleId;
        Vehicle? assignedVehicle = null;

        if (assignedVehicleId.HasValue)
        {
            assignedVehicle = await _vehicleRepository.GetByIdAsync(assignedVehicleId.Value);
        }
        else
        {
            assignedVehicle = userVehicles.FirstOrDefault(v => v.IsPrimary) ?? userVehicles.FirstOrDefault();
            assignedVehicleId = assignedVehicle?.Id;
        }

        // Verify vehicle does not already have an active reservation on this date
        if (assignedVehicleId.HasValue)
        {
            var vehicleReservations = (await _reservationRepository.GetAllAsync())
                .Where(r => r.VehicleId == assignedVehicleId.Value &&
                            r.ReservationDate.Year == request.ReservationDate.Year &&
                            r.ReservationDate.Month == request.ReservationDate.Month &&
                            r.ReservationDate.Day == request.ReservationDate.Day &&
                            r.Status != ReservationStatus.Cancelled &&
                            r.Status != ReservationStatus.Rejected)
                .ToList();

            if (vehicleReservations.Any())
            {
                return Result<ParkingReservationDto>.Failure(
                    $"This vehicle already has an active or pending reservation for {request.ReservationDate:MMMM dd, yyyy}. Only one reservation per day is allowed.",
                    ErrorCode.Conflict);
            }
        }

        // Generate Reference Number: RES-YYYYMMDD-XXXX
        var randomPart = new Random().Next(1000, 9999);
        var refNum = $"RES-{request.ReservationDate:yyyyMMdd}-{randomPart}";

        var endTimeToUse = request.Type == ReservationType.Special
            ? new TimeSpan(23, 59, 59)
            : request.EndTime;

        var reservation = new ParkingReservation(
            request.UserId,
            refNum,
            request.ReservationDate,
            request.StartTime,
            endTimeToUse,
            request.Reason,
            request.Type,
            assignedVehicleId
        );

        if (!string.IsNullOrWhiteSpace(request.NotifyEmail))
        {
            reservation.SetAdminNotes($"[NotifyEmail:{request.NotifyEmail.Trim()}]");
        }

        if (isAdmin)
        {
            var adminId = user.Id;
            reservation.Approve(adminId, "Auto-approved (Created by Admin)");
        }

        await _reservationRepository.AddAsync(reservation);
        await _reservationRepository.SaveChangesAsync();

        if (isAdmin)
        {
            try
            {
                var customNotifyEmail = !string.IsNullOrWhiteSpace(request.NotifyEmail)
                    ? request.NotifyEmail.Trim()
                    : null;
                var applicantEmail = customNotifyEmail ?? user.PrimaryEmail;
                var applicantName = user.UserProfile != null
                    ? $"{user.UserProfile.FirstName} {user.UserProfile.LastName}".Trim()
                    : "Applicant";

                if (!string.IsNullOrWhiteSpace(applicantEmail))
                {
                    var subject = $"✅ ParkFlow - Parking Reservation Approved ({reservation.ReferenceNumber})";
                    var reservationDateStr = reservation.ReservationDate.ToString("MMMM dd, yyyy");
                    var startTimeStr = DateTime.Today.Add(reservation.StartTime).ToString("hh:mm tt");
                    var endTimeStr = DateTime.Today.Add(reservation.EndTime).ToString("hh:mm tt");
                    var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={reservation.ReferenceNumber}";
                    var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #0f766e 100%);border-top:4px solid #f59e0b;padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:12px;'>
              PARKFLOW MANAGEMENT
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Parking Pass Approved!</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Official Campus Parking Entry Permit</p>
          </td>
        </tr>
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;line-height:1.6;color:#1e293b;margin:0 0 16px;'>Hello <strong>{applicantName}</strong>,</p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 24px;'>
              Your campus parking reservation request has been officially <strong style='color:#10b981;'>approved</strong>. Present the QR gate pass below to the guard scanner upon campus entry:
            </p>
            <div style='background-color:#f8fafc;border:2px dashed #cbd5e1;border-radius:14px;padding:28px 24px;text-align:center;margin-bottom:28px;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;letter-spacing:2px;text-transform:uppercase;margin-bottom:14px;'>OFFICIAL GATE PASS QR</div>
              <div style='display:inline-block;padding:12px;background:#ffffff;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.06);'>
                <img src='{qrUrl}' width='190' height='190' alt='Parking Pass QR Code' style='display:block;border:0;' />
              </div>
              <div style='margin-top:14px;'>
                <span style='font-family:monospace;font-size:18px;font-weight:800;color:#0f172a;letter-spacing:2px;background:#e2e8f0;padding:6px 14px;border-radius:6px;'>{reservation.ReferenceNumber}</span>
              </div>
            </div>
            <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Reservation Date</span><br>
                        <span style='font-size:15px;color:#0f172a;font-weight:700;'>{reservationDateStr}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Authorized Time Window</span><br>
                        <span style='font-size:15px;color:#10b981;font-weight:700;'>{startTimeStr} – {endTimeStr}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Purpose</span><br>
                        <span style='font-size:14px;color:#334155;'>{reservation.Reason}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Admin Remarks</span><br>
                        <span style='font-size:14px;color:#334155;font-style:italic;'>Auto-approved (Created by Admin)</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>
            <div style='text-align:center;margin-top:24px;padding-top:18px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:11px;color:#94a3b8;margin:0;'>ParkFlow • Office of Security & Safety</p>
            </div>
          </td>
        </tr>
        <tr>
          <td style='background-color:#f8fafc;padding:18px 36px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:11px;color:#94a3b8;margin:0;'>© {DateTime.UtcNow.Year} ParkFlow System. All rights reserved.</p>
          </td>
        </tr>
      </table>
    </td></tr>
  </table>
</body>
</html>";
                    await _emailService.SendEmailAsync(applicantEmail, subject, htmlBody);
                }
            }
            catch { }
        }

        var dto = new ParkingReservationDto
        {
            Id = reservation.Id,
            UserId = reservation.UserId,
            UserFullName = user.UserProfile != null ? $"{user.UserProfile.FirstName} {user.UserProfile.LastName}".Trim() : string.Empty,
            UserEmail = user.PrimaryEmail ?? string.Empty,
            ReferenceNumber = reservation.ReferenceNumber,
            ReservationDate = reservation.ReservationDate,
            StartTime = reservation.StartTime,
            EndTime = reservation.EndTime,
            Reason = reservation.Reason,
            Status = reservation.Status,
            Type = reservation.Type,
            VehicleId = reservation.VehicleId,
            PlateNumber = assignedVehicle?.PlateNumber,
            Brand = assignedVehicle?.Brand,
            VehicleQrCodeHash = assignedVehicle?.QrCodeHash,
            AdminNotes = reservation.AdminNotes,
            ApprovedAt = reservation.ApprovedAt,
            ApprovedByAdminId = reservation.ApprovedByAdminId,
            CreatedAt = reservation.CreatedAt
        };

        try
        {
            var eventData = new
            {
                id = reservation.Id,
                referenceNumber = reservation.ReferenceNumber,
                userId = reservation.UserId,
                userFullName = dto.UserFullName,
                plateNumber = dto.PlateNumber,
                reservationDate = reservation.ReservationDate,
                startTime = reservation.StartTime,
                endTime = reservation.EndTime,
                reason = reservation.Reason,
                status = reservation.Status.ToString(),
                type = reservation.Type.ToString(),
                createdAt = reservation.CreatedAt
            };

            await _notificationSender.SendToAllAsync("ReservationSubmitted", eventData);
            await _notificationSender.SendToAllAsync("ReservationUpdated", dto);
            await _notificationSender.SendToAllAsync("ApprovalListUpdated", new
            {
                type = "Reservation",
                id = reservation.Id,
                referenceNumber = reservation.ReferenceNumber,
                status = reservation.Status.ToString()
            });
        }
        catch
        {
            // Ignore SignalR broadcast error to prevent request failure
        }

        return Result<ParkingReservationDto>.Success(dto, "Parking reservation created successfully.");
    }
}
