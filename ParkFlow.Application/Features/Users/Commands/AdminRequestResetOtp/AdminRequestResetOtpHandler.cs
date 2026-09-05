using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Users.Commands.AdminRequestResetOtp;

public class AdminRequestResetOtpHandler : IRequestHandler<AdminRequestResetOtpCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IEmailService _emailService;

    public AdminRequestResetOtpHandler(
        IUserAccountRepository userAccountRepository,
        IEmailService emailService)
    {
        _userAccountRepository = userAccountRepository;
        _emailService = emailService;
    }

    public async Task<Result<string>> Handle(AdminRequestResetOtpCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TargetEmail))
            return Result<string>.Failure("Target user email is required.", ErrorCode.BadRequest);

        var targetEmailNormalized = request.TargetEmail.Trim().ToLower();
        var targetUser = await _userAccountRepository.GetByEmailAsync(targetEmailNormalized);
        if (targetUser is null)
            return Result<string>.Failure("Target user account not found.", ErrorCode.NotFound);

        // Determine Admin Email to send OTP code to
        string? adminEmail = request.AdminEmailOverride?.Trim().ToLower();
        if (string.IsNullOrWhiteSpace(adminEmail) && request.AdminUserId != Guid.Empty)
        {
            var adminUser = await _userAccountRepository.GetByIdAsync(request.AdminUserId);
            adminEmail = adminUser?.AuthIdentities?.FirstOrDefault()?.Email;
        }

        if (string.IsNullOrWhiteSpace(adminEmail))
        {
            adminEmail = targetEmailNormalized; // Fallback to target email if admin identity is not found
        }

        // Generate 6-digit random verification code
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        var codeHash = Sha256Base64(code);

        var expiresAt = DateTime.UtcNow.AddMinutes(10);
        targetUser.SetPasswordResetToken(codeHash, expiresAt);
        await _userAccountRepository.UpdateAsync(targetUser);

        // Send 6-digit verification code to the Admin Email
        var subject = $"[ParkFlow Security] Admin Password Reset Code for {targetEmailNormalized}";
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
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #dc2626 100%);border-top:4px solid #f59e0b;padding:32px 36px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:10px;'>
              PARKFLOW SECURITY
            </div>
            <h1 style='color:#ffffff;font-size:22px;font-weight:800;margin:0;'>Admin Authorization Code</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>Administrative Password Change Verification</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:32px 36px;'>
            <p style='font-size:14px;line-height:1.6;color:#334155;margin:0 0 16px;'>
              An administrator password reset was requested for user account: <strong style='color:#0f172a;'>{targetEmailNormalized}</strong>.
            </p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 20px;'>
              Use the authorization code below to complete administrative verification:
            </p>

            <div style='background-color:#f8fafc;border-left:4px solid #dc2626;border-radius:8px;padding:24px;text-align:center;margin:24px 0;border-top:1px solid #e2e8f0;border-right:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;text-transform:uppercase;letter-spacing:1.5px;margin-bottom:10px;'>Admin Authorization Code</div>
              <div style='font-size:36px;font-weight:900;letter-spacing:8px;color:#0f172a;font-family:monospace;'>{code}</div>
            </div>

            <p style='font-size:13px;line-height:1.6;color:#64748b;margin:0 0 20px;'>
              This code is valid for <strong>10 minutes</strong>. If you did not initiate this request, please review active admin session logs immediately.
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

        await _emailService.SendEmailAsync(adminEmail, subject, htmlBody);

        return Result<string>.Success(code, $"Verification code generated and sent to administrator email ({adminEmail}).");
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
