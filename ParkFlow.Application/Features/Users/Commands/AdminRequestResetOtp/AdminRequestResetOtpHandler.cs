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
        var subject = $"[ParkFlow Security] Password Change Verification Code for {targetEmailNormalized}";
        var htmlBody = $@"
            <div style='font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,sans-serif;max-width:600px;margin:0 auto;padding:24px;background-color:#f8fafc;color:#1e293b;'>
              <div style='background-color:#ffffff;border-radius:12px;padding:32px;border:1px solid #e2e8f0;box-shadow:0 4px 6px -1px rgba(0,0,0,0.05);'>
                <div style='text-align:center;margin-bottom:24px;'>
                  <div style='display:inline-block;padding:10px 18px;background-color:#dc2626;border-radius:8px;color:#ffffff;font-weight:800;font-size:18px;letter-spacing:1px;'>
                    PARKFLOW SECURITY
                  </div>
                </div>
                <h2 style='color:#0f172a;margin-top:0;font-size:20px;font-weight:700;text-align:center;'>Admin Password Reset Verification</h2>
                <p style='font-size:15px;line-height:1.6;color:#475569;'>An administrator password change request was initiated for account: <strong>{targetEmailNormalized}</strong>.</p>
                <p style='font-size:15px;line-height:1.6;color:#475569;'>Please use the 6-digit authorization code below to verify your admin identity and complete the password update:</p>

                <div style='background-color:#f1f5f9;border-left:4px solid #dc2626;padding:16px 20px;margin:24px 0;border-radius:6px;text-align:center;'>
                  <div style='font-size:12px;font-weight:700;color:#64748b;text-transform:uppercase;letter-spacing:1px;margin-bottom:8px;'>Authorization Code</div>
                  <div style='font-size:32px;font-weight:900;letter-spacing:6px;color:#0f172a;font-family:monospace;'>{code}</div>
                </div>

                <p style='font-size:13px;line-height:1.6;color:#64748b;'>This code will expire in <strong>10 minutes</strong>. If you did not initiate this change, please review active admin session logs immediately.</p>
                <div style='margin-top:28px;padding-top:20px;border-top:1px solid #e2e8f0;text-align:center;font-size:12px;color:#94a3b8;'>
                  © {DateTime.UtcNow.Year} Bulacan State University Security Office. All rights reserved.
                </div>
              </div>
            </div>";

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
