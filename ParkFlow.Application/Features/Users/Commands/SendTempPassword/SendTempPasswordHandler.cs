using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Commands.SendTempPassword;

public class SendTempPasswordHandler : IRequestHandler<SendTempPasswordCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public SendTempPasswordHandler(
        IUserAccountRepository userAccountRepository,
        IPasswordHasher passwordHasher,
        IEmailService emailService)
    {
        _userAccountRepository = userAccountRepository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<Result<string>> Handle(SendTempPasswordCommand request, CancellationToken cancellationToken)
    {
        var targetEmail = !string.IsNullOrWhiteSpace(request.TargetEmail)
            ? request.TargetEmail.Trim()
            : request.Email?.Trim();

        if (string.IsNullOrWhiteSpace(targetEmail))
            return Result<string>.Failure("Target user email is required.", ErrorCode.BadRequest);

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword))
            return Result<string>.Failure("Temporary password is required.", ErrorCode.BadRequest);

        var user = await _userAccountRepository.GetByEmailAsync(targetEmail);
        if (user == null)
            return Result<string>.Failure("User account not found.", ErrorCode.NotFound);

        // Hash and update password
        var passwordHash = _passwordHasher.HashPassword(request.TemporaryPassword);
        user.UpdatePassword(passwordHash);

        var manualIdentity = user.AuthIdentities.FirstOrDefault(i => i.Provider == AuthProvider.Manual);
        if (manualIdentity != null)
        {
            manualIdentity.UpdatePasswordHash(passwordHash);
        }
        else
        {
            var newIdentity = AuthIdentity.CreateManual(user.Id, targetEmail, passwordHash, true);
            user.AuthIdentities.Add(newIdentity);
        }

        user.PasswordHistories.Add(new PasswordHistory(user.Id, passwordHash));
        await _userAccountRepository.UpdateAsync(user);

        // Send Temporary Password Email
        var recipientName = user.UserProfile != null
            ? $"{user.UserProfile.FirstName} {user.UserProfile.LastName}".Trim()
            : "ParkFlow User";

        var subject = "[ParkFlow Security] Your Account Temporary Login Password";
        var htmlBody = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='margin:0;padding:0;background-color:#f8fafc;font-family:-apple-system,BlinkMacSystemFont,""Segoe UI"",Roboto,Helvetica,Arial,sans-serif;'>
  <table width='100%' cellpadding='0' cellspacing='0' style='background-color:#f8fafc;padding:40px 16px;'>
    <tr><td align='center'>
      <table width='580' cellpadding='0' cellspacing='0' style='background-color:#ffffff;border-radius:16px;overflow:hidden;box-shadow:0 10px 25px rgba(0,0,0,0.08);border:1px solid #e2e8f0;'>
        <!-- Header -->
        <tr>
          <td style='background:linear-gradient(135deg, #1e293b 0%, #0f172a 100%);border-top:4px solid #D22730;padding:32px 36px;text-align:center;'>
            <div style='display:inline-block;padding:4px 14px;background:rgba(210,39,48,0.18);border:1px solid rgba(210,39,48,0.4);border-radius:20px;color:#f87171;font-size:11px;font-weight:800;letter-spacing:2px;text-transform:uppercase;margin-bottom:10px;'>
              PARKFLOW SECURITY
            </div>
            <h1 style='color:#ffffff;font-size:22px;font-weight:800;margin:0;'>Temporary Login Password</h1>
            <p style='color:rgba(255,255,255,0.8);font-size:13px;margin:6px 0 0;'>Administrative Credential Reset</p>
          </td>
        </tr>

        <!-- Content -->
        <tr>
          <td style='padding:32px 36px;'>
            <p style='font-size:14px;line-height:1.6;color:#334155;margin:0 0 16px;'>
              Hello <strong>{recipientName}</strong>,
            </p>
            <p style='font-size:14px;line-height:1.6;color:#475569;margin:0 0 20px;'>
              An administrator has generated a new temporary password for your ParkFlow account (<strong>{targetEmail}</strong>). You can use this temporary password to log in immediately.
            </p>

            <div style='background-color:#f8fafc;border-left:4px solid #D22730;border-radius:8px;padding:20px;text-align:center;margin:24px 0;border-top:1px solid #e2e8f0;border-right:1px solid #e2e8f0;border-bottom:1px solid #e2e8f0;'>
              <div style='font-size:11px;font-weight:800;color:#64748b;text-transform:uppercase;letter-spacing:1.5px;margin-bottom:10px;'>Your Temporary Password</div>
              <div style='font-size:28px;font-weight:900;letter-spacing:4px;color:#0f172a;font-family:monospace;'>{request.TemporaryPassword}</div>
            </div>

            <p style='font-size:13px;line-height:1.6;color:#64748b;margin:0 0 12px;'>
              <strong>Important:</strong> For your account security, we strongly recommend changing this password immediately upon logging in via <em>Account Settings &gt; Security &amp; Password</em>.
            </p>

            <div style='text-align:center;margin-top:24px;padding-top:18px;border-top:1px solid #e2e8f0;'>
              <p style='font-size:11px;color:#94a3b8;margin:0;'>ParkFlow • Campus Parking Management System</p>
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

        try
        {
            await _emailService.SendEmailAsync(targetEmail, subject, htmlBody);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Password was updated, but failed to send email: {ex.Message}", ErrorCode.ServerError);
        }

        return Result<string>.Success(request.TemporaryPassword, $"Temporary password has been generated, applied, and emailed to {targetEmail}.");
    }
}
