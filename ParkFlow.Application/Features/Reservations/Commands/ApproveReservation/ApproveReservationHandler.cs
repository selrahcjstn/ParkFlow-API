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
                    var subject = $"✅ Parking Reservation Approved – {reservation.ReferenceNumber}";
                    var reservationDate = reservation.ReservationDate.ToString("MMMM dd, yyyy");
                    var startTime = DateTime.Today.Add(reservation.StartTime).ToString("hh:mm tt");
                    var endTime = DateTime.Today.Add(reservation.EndTime).ToString("hh:mm tt");
                    var notes = string.IsNullOrWhiteSpace(request.Notes) ? "No additional remarks." : request.Notes;

                    var qrUrl = $"https://api.qrserver.com/v1/create-qr-code/?size=220x220&data={reservation.ReferenceNumber}";
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
          <td style='background:linear-gradient(135deg,#10b981,#059669);padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(255,255,255,0.2);border-radius:20px;color:#ffffff;font-size:11px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:12px;'>
              APPROVED PARKING PERMIT & PASS
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Reservation Approved!</h1>
            <p style='color:rgba(255,255,255,0.9);font-size:14px;margin:8px 0 0;'>Hi {applicantName}, your parking pass is active.</p>
          </td>
        </tr>

        <!-- Body Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:14px;color:#475569;margin:0 0 24px;line-height:1.6;'>
              Your parking reservation request has been officially <strong style='color:#10b981;'>approved</strong>. Present the QR pass below at the gate scanner upon arrival:
            </p>

            <!-- QR Pass Card Ticket -->
            <div style='background:#f8fafc;border:2px dashed #cbd5e1;border-radius:14px;padding:28px 24px;text-align:center;margin-bottom:28px;'>
              <div style='font-size:11px;font-weight:700;color:#64748b;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:14px;'>OFFICIAL GATE ENTRY QR PASS</div>
              <div style='display:inline-block;padding:12px;background:#ffffff;border-radius:12px;box-shadow:0 4px 12px rgba(0,0,0,0.06);'>
                <img src='{qrUrl}' width='190' height='190' alt='Parking Pass QR Code' style='display:block;border:0;' />
              </div>
              <div style='margin-top:14px;'>
                <span style='font-family:monospace;font-size:18px;font-weight:800;color:#0f172a;letter-spacing:2px;background:#e2e8f0;padding:4px 12px;border-radius:6px;'>{reservation.ReferenceNumber}</span>
              </div>
              <p style='font-size:12px;color:#64748b;margin:10px 0 0;'>Valid for single entry during your reserved slot.</p>
            </div>

            <!-- Details List -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f1f5f9;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Reservation Date</span><br>
                        <span style='font-size:15px;color:#0f172a;font-weight:700;'>{reservationDate}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Authorized Time Window</span><br>
                        <span style='font-size:15px;color:#10b981;font-weight:700;'>{startTime} – {endTime}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Purpose</span><br>
                        <span style='font-size:14px;color:#334155;'>{reservation.Reason}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:700;text-transform:uppercase;letter-spacing:0.5px;'>Admin Remarks</span><br>
                        <span style='font-size:14px;color:#334155;font-style:italic;'>{notes}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <p style='font-size:12px;color:#94a3b8;text-align:center;margin:0;'>
              ParkFlow Parking Management System • Approved Gate Pass
            </p>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background:#f8fafc;padding:20px 40px;text-align:center;border-top:1px solid #e2e8f0;'>
            <p style='font-size:12px;color:#94a3b8;margin:0;'>This is an automated notification from ParkFlow. Please do not reply to this email.</p>
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
