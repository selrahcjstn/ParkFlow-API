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
          <td style='background:linear-gradient(135deg,#6366f1,#4f46e5);padding:32px 40px;text-align:center;'>
            <div style='font-size:40px;margin-bottom:8px;'>📢</div>
            <h1 style='color:#ffffff;font-size:22px;font-weight:700;margin:0;'>Special Parking Schedule Notice</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:14px;margin:8px 0 0;'>An admin has reserved a special parking schedule.</p>
          </td>
        </tr>

        <!-- Body -->
        <tr>
          <td style='padding:36px 40px;'>
            <p style='font-size:14px;color:#6b7280;margin:0 0 28px;line-height:1.6;'>
              Please be advised of the following special parking schedule reservation that has been created by the administration:
            </p>

            <!-- Details Card -->
            <table width='100%' cellpadding='0' cellspacing='0' style='background:#f9fafb;border:1px solid #e5e7eb;border-radius:10px;margin-bottom:28px;'>
              <tr>
                <td style='padding:20px 24px;'>
                  <table width='100%' cellpadding='0' cellspacing='0'>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e5e7eb;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Reference Number</span><br>
                        <span style='font-size:15px;color:#111827;font-weight:700;font-family:monospace;'>{refNum}</span>
                      </td>
                    </tr>
                    <tr>
                      <td style='padding:8px 0;border-bottom:1px solid #e5e7eb;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Schedule Date</span><br>
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
                      <td style='padding:8px 0;'>
                        <span style='font-size:12px;color:#9ca3af;font-weight:600;text-transform:uppercase;letter-spacing:0.5px;'>Purpose / Event</span><br>
                        <span style='font-size:15px;color:#111827;'>{reservation.Reason}</span>
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>

            <p style='font-size:13px;color:#9ca3af;text-align:center;margin:0;'>
              For questions, please contact the parking office.<br>
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
