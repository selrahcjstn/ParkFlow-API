using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Commands.ApproveReservation;

public class ApproveReservationHandler : IRequestHandler<ApproveReservationCommand, Result<bool>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IEmailService _emailService;

    public ApproveReservationHandler(
        IParkingReservationRepository reservationRepository,
        ISignalRNotificationSender notificationSender,
        IEmailService emailService)
    {
        _reservationRepository = reservationRepository;
        _notificationSender = notificationSender;
        _emailService = emailService;
    }

    public async Task<Result<bool>> Handle(ApproveReservationCommand request, CancellationToken cancellationToken)
    {
        var reservation = await _reservationRepository.GetByIdAsync(request.ReservationId);
        if (reservation == null)
            return Result<bool>.Failure("Reservation not found.", ErrorCode.NotFound);

        try
        {
            string? customNotifyEmail = null;
            if (!string.IsNullOrWhiteSpace(reservation.AdminNotes) && reservation.AdminNotes.Contains("[NotifyEmail:"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(reservation.AdminNotes, @"\[NotifyEmail:(.*?)\]");
                if (match.Success)
                {
                    customNotifyEmail = match.Groups[1].Value.Trim();
                }
            }

            reservation.Approve(request.AdminId, request.Notes);
            await _reservationRepository.UpdateAsync(reservation);
            await _reservationRepository.SaveChangesAsync();

            try
            {
                await _notificationSender.SendToAllAsync("ReservationUpdated", new { id = reservation.Id, status = "Approved" });
            }
            catch { }

            // Send email notification to the applicant
            try
            {
                var applicantEmail = !string.IsNullOrWhiteSpace(customNotifyEmail)
                    ? customNotifyEmail
                    : reservation.UserAccount?.PrimaryEmail;

                var applicantName = reservation.UserAccount?.UserProfile != null
                    ? $"{reservation.UserAccount.UserProfile.FirstName} {reservation.UserAccount.UserProfile.LastName}".Trim()
                    : "Applicant";

                if (!string.IsNullOrWhiteSpace(applicantEmail))
                {
                    var subject = $"✅ BulSU ParkFlow - Parking Reservation Approved ({reservation.ReferenceNumber})";
                    var reservationDate = reservation.ReservationDate.ToString("MMMM dd, yyyy");
                    var startTime = DateTime.Today.Add(reservation.StartTime).ToString("hh:mm tt");
                    var endTime = DateTime.Today.Add(reservation.EndTime).ToString("hh:mm tt");
                    var notes = string.IsNullOrWhiteSpace(request.Notes) ? "No additional remarks." : request.Notes;

                    var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={reservation.ReferenceNumber}";
                    var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- BulSU Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #0f766e 100%);border-top:4px solid #f59e0b;padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:12px;'>
              BULACAN STATE UNIVERSITY
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Parking Pass Approved!</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Official Campus Parking Entry Permit</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;line-height:1.6;color:#1e293b;margin:0 0 16px;'>Hello <strong>{applicantName}</strong>,</p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 24px;'>
              Your campus parking reservation request has been officially <strong style='color:#10b981;'>approved</strong>. Present the QR gate pass below to the guard scanner upon campus entry:
            </p>

            <!-- QR Pass Box -->
            <div style='background-color:#f8fafc;border:2px dashed #cbd5e1;border-radius:14px;padding:28px 24px;text-align:center;margin-bottom:28px;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;letter-spacing:2px;text-transform:uppercase;margin-bottom:14px;'>OFFICIAL CAMPUS GATE PASS QR</div>
              <div style='display:inline-block;padding:12px;background:#ffffff;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.06);'>
                <img src='{qrUrl}' width='190' height='190' alt='Parking Pass QR Code' style='display:block;border:0;' />
              </div>
              <div style='margin-top:14px;'>
                <span style='font-family:monospace;font-size:18px;font-weight:800;color:#0f172a;letter-spacing:2px;background:#e2e8f0;padding:6px 14px;border-radius:6px;'>{reservation.ReferenceNumber}</span>
              </div>
            </div>

            <!-- Reservation Details -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f8fafc;border:1px solid #e2e8f0;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Reservation Date</span><br>
                        <span style='font-size:15px;color:#0f172a;font-weight:700;'>{reservationDate}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Authorized Time Window</span><br>
                        <span style='font-size:15px;color:#10b981;font-weight:700;'>{startTime} – {endTime}</span>
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
                        <span style='font-size:14px;color:#334155;font-style:italic;'>{notes}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <div style='text-align:center;margin-top:24px;padding-top:18px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:11px;color:#94a3b8;margin:0;'>Bulacan State University • Office of Security & Safety</p>
            </div>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background-color:#f8fafc;padding:18px 36px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:11px;color:#94a3b8;margin:0;'>© {DateTime.UtcNow.Year} Bulacan State University ParkFlow System. All rights reserved.</p>
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
            catch { /* Don't fail the approval if email sending fails */ }

            return Result<bool>.Success(true, "Reservation approved successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest);
        }
    }
}
