using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;

public class ForgotPasswordUserAccountHandler
    : IRequestHandler<ForgotPasswordUserAccountCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IEmailService _emailService;
    private readonly IValidator<ForgotPasswordUserAccountCommand> _validator;

    public ForgotPasswordUserAccountHandler(
        IUserAccountRepository userAccountRepository,
        IEmailService emailService,
        IValidator<ForgotPasswordUserAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _emailService = emailService;
        _validator = validator;
    }

    public async Task<Result<string>> Handle(ForgotPasswordUserAccountCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<string>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userAccountRepository.GetByEmailAsync(request.Email);

        if (user is null)
            return Result<string>.Failure("User account not found.", ErrorCode.NotFound);

        var manualIdentity = user.AuthIdentities.FirstOrDefault(i =>
            i.Provider == AuthProvider.Manual &&
            i.Email != null &&
            i.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));

        if (manualIdentity == null || string.IsNullOrWhiteSpace(manualIdentity.PasswordHash))
            return Result<string>.Failure("Password reset is only available for manual accounts.", ErrorCode.BadRequest);

        // Generate a random 6-digit verification code
        var random = new Random();
        var code = random.Next(100000, 999999).ToString();
        var codeHash = Sha256Base64(code);

        // Code expires in exactly 10 minutes
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        user.SetPasswordResetToken(codeHash, expiresAt);
        await _userAccountRepository.UpdateAsync(user);

        // Send reset code via email
        var subject = "BulSU ParkFlow - Password Reset Request";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f1f5f9;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f1f5f9;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='580' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- BulSU Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #7f1d1d 0%, #0f172a 60%, #0f766e 100%);border-top:4px solid #f59e0b;padding:32px 36px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(245,158,11,0.18);border:1px solid rgba(245,158,11,0.4);border-radius:20px;color:#fbbf24;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:10px;'>
              BULACAN STATE UNIVERSITY
            </div>
            <h1 style='color:#ffffff;font-size:22px;font-weight:800;margin:0;'>Password Reset Verification</h1>
            <p style='color:rgba(255,255,255,0.85);font-size:13px;margin:6px 0 0;'>ParkFlow Account Security</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:32px 36px;'>
            <p style='font-size:14px;line-height:1.6;color:#334155;margin:0 0 20px;'>
              We received a request to reset your password for your BulSU ParkFlow account.
            </p>

            <div style='background-color:#f8fafc;border-left:4px solid #dc2626;border-radius:8px;padding:24px;text-align:center;margin:24px 0;border-top:1px solid #e2e8f0;border-right:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;text-transform:uppercase;letter-spacing:1.5px;margin-bottom:10px;'>Your 6-Digit Authorization Code</div>
              <div style='font-size:36px;font-weight:900;letter-spacing:8px;color:#0f172a;font-family:monospace;'>{code}</div>
            </div>

            <p style='font-size:13px;line-height:1.6;color:#64748b;margin:0 0 20px;'>
              This code will expire in <strong>10 minutes</strong>. If you did not request a password reset, please secure your account immediately or ignore this email.
            </p>

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

        await _emailService.SendEmailAsync(request.Email, subject, htmlBody);

        return Result<string>.Success(code, "Password reset verification code generated and sent via email.");
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
