using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.SendEmailOtp;

public class SendEmailOtpCommandHandler : IRequestHandler<SendEmailOtpCommand, Result<bool>>
{
    private readonly IEmailOtpRepository _emailOtpRepository;
    private readonly IEmailService _emailService;
    private readonly IValidator<SendEmailOtpCommand> _validator;

    public SendEmailOtpCommandHandler(
        IEmailOtpRepository emailOtpRepository,
        IEmailService emailService,
        IValidator<SendEmailOtpCommand> validator)
    {
        _emailOtpRepository = emailOtpRepository;
        _emailService = emailService;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(SendEmailOtpCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        try
        {
            // Generate a random 6-digit OTP code
            var random = new Random();
            var otpCode = random.Next(100000, 999999).ToString();

            // Set expiration to 5 minutes from creation
            var expiresAt = DateTime.UtcNow.AddMinutes(5);

            // Save the OTP in the EmailOtps table
            var emailOtp = new EmailOtp(request.Email, otpCode, expiresAt);
            await _emailOtpRepository.AddAsync(emailOtp);

            // Send the OTP via email
            var subject = "ParkFlow - Email Verification OTP Code";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='580' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #0f766e 100%);border-top:4px solid #f59e0b;padding:32px 36px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:10px;'>
              PARKFLOW MANAGEMENT
            </div>
            <h1 style='color:#ffffff;font-size:22px;font-weight:800;margin:0;'>ParkFlow Email Verification</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>One-Time Password (OTP) Authorization</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:32px 36px;'>
            <p style='font-size:14px;line-height:1.6;color:#334155;margin:0 0 20px;'>
              You requested a One-Time Password (OTP) to verify your email address on the ParkFlow system.
            </p>

            <div style='background-color:#f8fafc;border-left:4px solid #f59e0b;border-radius:8px;padding:24px;text-align:center;margin:24px 0;border-top:1px solid #e2e8f0;border-right:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;text-transform:uppercase;letter-spacing:1.5px;margin-bottom:10px;'>Your 6-Digit Verification Code</div>
              <div style='font-size:36px;font-weight:900;letter-spacing:8px;color:#0f172a;font-family:monospace;'>{otpCode}</div>
            </div>

            <p style='font-size:13px;line-height:1.6;color:#64748b;margin:0 0 20px;'>
              This code is valid for exactly <strong>5 minutes</strong>. Do not share this code with anyone. If you did not request this verification code, please disregard this message.
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

            await _emailService.SendEmailAsync(request.Email, subject, htmlBody);

            return Result<bool>.Success(true, "OTP code generated and sent successfully.");
        }
        catch (Exception ex)
        {
            return Result<bool>.Failure(false, $"Failed to send OTP: {ex.Message}", ErrorCode.ServerError);
        }
    }
}
