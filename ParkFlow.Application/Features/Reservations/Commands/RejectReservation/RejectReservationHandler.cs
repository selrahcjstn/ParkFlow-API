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
                var applicantEmail = reservation.UserAccount?.PrimaryEmail;
                var applicantName = reservation.UserAccount?.UserProfile != null
                    ? $"{reservation.UserAccount.UserProfile.FirstName} {reservation.UserAccount.UserProfile.LastName}".Trim()
                    : "Applicant";

                if (!string.IsNullOrWhiteSpace(applicantEmail))
                {
                    var subject = $"❌ Parking Reservation Declined – {reservation.ReferenceNumber}";
                    var reservationDate = reservation.ReservationDate.ToString("MMMM dd, yyyy");
                    var startTime = DateTime.Today.Add(reservation.StartTime).ToString("hh:mm tt");
                    var endTime = DateTime.Today.Add(reservation.EndTime).ToString("hh:mm tt");
                    var notes = string.IsNullOrWhiteSpace(request.Notes) ? "No reason provided." : request.Notes;

                    var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background:#f4f6f9;font-family:Inter,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background:#f4f6f9;padding:32px 0;'>
    <tr><td align='center'>
      <table width='560' cellpadding='0' cellspacing='0' style='background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 4px 24px rgba(0,0,0,0.08);'>

        <!-- Header -->
        <tr>
          <td style='background:linear-gradient(135deg,#ef4444,#dc2626);padding:32px 40px;text-align:center;'>
            <div style='font-size:40px;margin-bottom:8px;'>❌</div>
            <h1 style='color:#ffffff;font-size:22px;font-weight:700;margin:0;'>Reservation Declined</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:14px;margin:8px 0 0;'>Your parking reservation request was not approved.</p>
          </td>
        </tr>

        <!-- Body -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:15px;color:#374151;margin:0 0 24px;'>Hi <strong>{applicantName}</strong>,</p>
            <p style='font-size:14px;color:#6b7280;margin:0 0 28px;line-height:1.6;'>
              We regret to inform you that your parking reservation request has been <strong style='color:#ef4444;'>declined</strong> by the admin. Here are the details of the request:
            </p>

            <!-- Details Card -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f9fafb;border:1px solid #e5e7eb;border-radius:10px;margin-bottom:28px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e5e7eb;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Reference Number</span><br>
                        <span style='font-size:15px;color:#111827;font-weight:700;font-family:monospace;'>{reservation.ReferenceNumber}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e5e7eb;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Reservation Date</span><br>
                        <span style='font-size:15px;color:#111827;font-weight:600;'>{reservationDate}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e5e7eb;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Time Slot</span><br>
                        <span style='font-size:15px;color:#111827;font-weight:600;'>{startTime} – {endTime}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e5e7eb;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Purpose</span><br>
                        <span style='font-size:15px;color:#111827;'>{reservation.Reason}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Reason for Decline</span><br>
                        <span style='font-size:14px;color:#374151;font-style:italic;'>{notes}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <p style='font-size:14px;color:#6b7280;line-height:1.6;margin:0 0 16px;'>
              You may submit a new reservation request through the ParkFlow mobile app. If you believe this was an error, please contact the parking office directly.
            </p>

            <p style='font-size:13px;color:#9ca3af;text-align:center;margin:0;'>
              — ParkFlow Parking Management System
            </p>
          </td>
        </tr>

        <!-- Footer -->
        <tr>
          <td style='background:#f9fafb;padding:20px 40px;text-align:center;border-top:1px solid #e5e7eb;'>
            <p style='font-size:12px;color:#9ca3af;margin:0;'>This is an automated notification from ParkFlow. Please do not reply to this email.</p>
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
