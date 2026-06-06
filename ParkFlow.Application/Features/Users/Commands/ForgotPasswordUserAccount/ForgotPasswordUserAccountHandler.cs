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
        var subject = "ParkFlow Password Reset Request";
        var htmlBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; color: #333;'>
                <h2>ParkFlow Password Reset</h2>
                <p>We received a request to reset your password. Use the following 6-digit verification code to proceed:</p>
                <div style='background-color: #f4f4f4; padding: 15px; border-radius: 5px; font-size: 24px; font-weight: bold; letter-spacing: 2px; text-align: center; margin: 20px 0;'>
                    {code}
                </div>
                <p>This verification code is valid for exactly <strong>10 minutes</strong>. If you did not request a password reset, please ignore this email.</p>
                <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;' />
                <p style='font-size: 12px; color: #888;'>This is an automated email from ParkFlow. Please do not reply.</p>
            </div>";

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
