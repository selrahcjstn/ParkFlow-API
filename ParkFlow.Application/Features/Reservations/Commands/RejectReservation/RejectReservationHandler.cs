using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Reservations.Commands.RejectReservation;

public class RejectReservationHandler : IRequestHandler<RejectReservationCommand, Result<bool>>
{
    private readonly IParkingReservationRepository _reservationRepository;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly IEmailService _emailService;

    public RejectReservationHandler(
        IParkingReservationRepository reservationRepository,
        ISignalRNotificationSender notificationSender,
        IEmailService emailService)
    {
        _reservationRepository = reservationRepository;
        _notificationSender = notificationSender;
        _emailService = emailService;
    }

    public async Task<Result<bool>> Handle(RejectReservationCommand request, CancellationToken cancellationToken)
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

            reservation.Reject(request.AdminId, request.Notes);
            await _reservationRepository.UpdateAsync(reservation);
            await _reservationRepository.SaveChangesAsync();

            try
            {
                await _notificationSender.SendToAllAsync("ReservationUpdated", new { id = reservation.Id, status = "Rejected" });
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
                    var subject = $"❌ ParkFlow - Parking Reservation Declined ({reservation.ReferenceNumber})";
                    var reservationDate = reservation.ReservationDate.ToString("MMMM dd, yyyy");
                    var startTime = DateTime.Today.Add(reservation.StartTime).ToString("hh:mm tt");
                    var endTime = DateTime.Today.Add(reservation.EndTime).ToString("hh:mm tt");
                    var notes = string.IsNullOrWhiteSpace(request.Notes) ? "No reason provided." : request.Notes;

                    var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='600' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #dc2626 100%);border-top:4px solid #f59e0b;padding:36px 40px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:12px;'>
              PARKFLOW MANAGEMENT
            </div>
            <h1 style='color:#ffffff;font-size:24px;font-weight:800;margin:0;letter-spacing:-0.5px;'>Reservation Request Declined</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Campus Parking Management Notice</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;line-height:1.6;color:#1e293b;margin:0 0 16px;'>Hello <strong>{applicantName}</strong>,</p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 24px;'>
              We regret to inform you that your campus parking reservation request has been <strong style='color:#ef4444;'>declined</strong> by the administration.
            </p>

            <!-- Details Box -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f8fafc;border:1px solid #e2e8f0;border-left:4px solid #dc2626;border-radius:12px;margin-bottom:24px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Reference Number</span><br>
                        <span style='font-size:15px;color:#0f172a;font-weight:800;font-family:monospace;'>{reservation.ReferenceNumber}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Reservation Date</span><br>
                        <span style='font-size:15px;color:#0f172a;font-weight:700;'>{reservationDate}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e2e8f0;'>
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Time Slot</span><br>
                        <span style='font-size:15px;color:#334155;font-weight:700;'>{startTime} – {endTime}</span>
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
                        <span style='font-size:11px;color:#64748b;font-weight:800;text-transform:uppercase;letter-spacing:0.5px;'>Reason for Decline</span><br>
                        <span style='font-size:14px;color:#ef4444;font-weight:600;font-style:italic;'>{notes}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <p style='font-size:13px;line-height:1.6;color:#64748b;margin:0 0 20px;'>
              You may submit a new reservation request via the ParkFlow mobile app or contact campus security for assistance.
            </p>

            <div style='text-align:center;margin-top:24px;padding-top:18px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:11px;color:#94a3b8;margin:0;'>ParkFlow • Office of Security & Safety</p>
            </div>
          </td>
        </tr>

        <!-- Footer -->
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
            catch { /* Don't fail the rejection if email sending fails */ }

            return Result<bool>.Success(true, "Reservation rejected successfully.");
        }
        catch (InvalidOperationException ex)
        {
            return Result<bool>.Failure(ex.Message, ErrorCode.BadRequest);
        }
    }
}
