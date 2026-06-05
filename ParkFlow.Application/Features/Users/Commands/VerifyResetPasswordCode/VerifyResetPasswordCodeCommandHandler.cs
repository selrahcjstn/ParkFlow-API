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

namespace ParkFlow.Application.Features.Users.Commands.VerifyResetPasswordCode;

public class VerifyResetPasswordCodeCommandHandler
    : IRequestHandler<VerifyResetPasswordCodeCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<VerifyResetPasswordCodeCommand> _validator;

    public VerifyResetPasswordCodeCommandHandler(
        IUserAccountRepository userAccountRepository,
        IValidator<VerifyResetPasswordCodeCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _validator = validator;
    }

    public async Task<Result<string>> Handle(VerifyResetPasswordCodeCommand request, CancellationToken cancellationToken)
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

        var codeHash = Sha256Base64(request.Code);
        var utcNow = DateTime.UtcNow;

        if (!user.CanResetPasswordWithToken(codeHash, utcNow))
            return Result<string>.Failure("Invalid or expired verification code.", ErrorCode.Unauthorized);

        // Code verified successfully! Now exchange it for a secure single-use reset token
        var secureResetToken = GenerateResetToken();
        var secureResetTokenHash = Sha256Base64(secureResetToken);
        var expiresAt = DateTime.UtcNow.AddMinutes(15); // Reset token lasts 15 minutes

        user.SetPasswordResetToken(secureResetTokenHash, expiresAt);
        await _userAccountRepository.UpdateAsync(user);

        return Result<string>.Success(secureResetToken, "Verification successful. Reset token generated.");
    }

    private static string GenerateResetToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes)
    {
        var base64 = Convert.ToBase64String(bytes);
        return base64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
