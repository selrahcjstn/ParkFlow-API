using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationHandler : IRequestHandler<CreateReservationCommand, Result<ParkingReservationDto>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly IUserAccountRepository _userRepository;
    private readonly IValidator<CreateReservationCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IEmailService _emailService;

    public CreateReservationHandler(
        IParkingReservationRepository reservationRepository,
        IUserAccountRepository userRepository,
        IValidator<CreateReservationCommand> validator,
        ISignalRNotificationSender notificationSender,
        IEmailService emailService)
    {
        _reservationRepository = reservationRepository;
        _userRepository = userRepository;
        _validator = validator;
        _notificationSender = notificationSender;
        _emailService = emailService;
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

        // Generate Reference Number: RES-YYYYMMDD-XXXX
        var randomPart = new Random().Next(1000, 9999);
        var refNum = $"RES-{request.ReservationDate:yyyyMMdd}-{randomPart}";

        var reservation = new ParkingReservation(
            request.UserId,
            refNum,
            request.ReservationDate,
            request.StartTime,
            request.EndTime,
            request.Reason
        );

        await _reservationRepository.AddAsync(reservation);
        await _reservationRepository.SaveChangesAsync();

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
            AdminNotes = reservation.AdminNotes,
            ApprovedAt = reservation.ApprovedAt,
            ApprovedByAdminId = reservation.ApprovedByAdminId,
            CreatedAt = reservation.CreatedAt
        };

        try
        {
            await _notificationSender.SendToAllAsync("ReservationUpdated", dto);
        }
        catch
        {
            // Ignore SignalR broadcast error to prevent request failure
        }

        // Send optional email notification if an address was provided
        if (!string.IsNullOrWhiteSpace(request.NotifyEmail))
        {
            try
            {
                var reservationDate = reservation.ReservationDate.ToString("MMMM dd, yyyy");
                var startTime = DateTime.Today.Add(reservation.StartTime).ToString("hh:mm tt");
                var endTime = DateTime.Today.Add(reservation.EndTime).ToString("hh:mm tt");

                var subject = $"📢 Special Parking Schedule Notice – {reservationDate}";
                var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={refNum}";
                var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#0f172a;font-family:Inter,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#0f172a;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='580' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 20px 40px rgba(0,0,0,0.25);'>

        <!-- Pass Header -->
        <tr>
          <td style='background:linear-gradient(135deg,#4f46e5,#7c3aed);padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(255,255,255,0.2);border-radius:20px;color:#ffffff;font-size:11px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:12px;'>
              OFFICIAL PARKING PERMIT & PASS
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Special Parking Schedule Notice</h1>
            <p style='color:rgba(255,255,255,0.9);font-size:14px;margin:8px 0 0;'>Administrative Schedule Reservation</p>
          </td>
        </tr>

        <!-- Body Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:14px;color:#475569;margin:0 0 24px;line-height:1.6;'>
              A special parking schedule reservation has been issued by administration. Please find your official entry pass details and scannable QR permit below:
            </p>

            <!-- QR Pass Card Ticket -->
            <div style='background:#f8fafc;border:2px dashed #cbd5e1;border-radius:14px;padding:28px 24px;text-align:center;margin-bottom:28px;'>
              <div style='font-size:11px;font-weight:700;color:#64748b;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:14px;'>GATE SCANNER QR PASS</div>
              <div style='display:inline-block;padding:12px;background:#ffffff;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.06);'>
                <img src='{qrUrl}' width='190' height='190' alt='Parking Pass QR Code' style='display:block;border:0;' />
              </div>
              <div style='margin-top:14px;'>
                <span style='font-family:monospace;font-size:18px;font-weight:800;color:#0f172a;letter-spacing:2px;background:#e2e8f0;padding:4px 12px;border-radius:6px;'>{refNum}</span>
              </div>
              <p style='font-size:12px;color:#64748b;margin:10px 0 0;'>Scan at the campus barrier gate scanner for authorized entry.</p>
            </div>

            <!-- Details List -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Schedule Date</span><br>
                        <span style='font-size:15px;color:#0f172a;font-weight:700;'>{reservationDate}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Authorized Time Slot</span><br>
                        <span style='font-size:15px;color:#4f46e5;font-weight:700;'>{startTime} – {endTime}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Event / Purpose</span><br>
                        <span style='font-size:14px;color:#334155;'>{reservation.Reason}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <p style='font-size:12px;color:#94a3b8;text-align:center;margin:0;'>
              ParkFlow Parking Management System • Authorized Special Pass
            </p>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background:#f8fafc;padding:20px 40px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:12px;color:#94a3b8;margin:0;'>This is an automated special schedule notification from ParkFlow.</p>
          </td>
        </tr>

      </table>
    </td></tr>
  </table>
</body>
</html>";

                await _emailService.SendEmailAsync(request.NotifyEmail, subject, htmlBody);
            }
            catch
            {
                // Don't fail the reservation creation if email fails
            }
        }

        return Result<ParkingReservationDto>.Success(dto, "Parking reservation created successfully.");
    }
}
