using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace ParkFlow.Application.Features.Users.Commands.ResetPasswordUserAccount;

public class ResetPasswordUserAccountHandler
    : IRequestHandler<ResetPasswordUserAccountCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<ResetPasswordUserAccountCommand> _validator;
    private readonly IPasswordHasher _passwordHasher;

    public ResetPasswordUserAccountHandler(
        IUserAccountRepository userAccountRepository,
        IValidator<ResetPasswordUserAccountCommand> validator,
        IPasswordHasher passwordHasher)
    {
        _userAccountRepository = userAccountRepository;
        _validator = validator;
        _passwordHasher = passwordHasher;
    }

    public async Task<Result<Guid>> Handle(ResetPasswordUserAccountCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var validationResult = await _validator.ValidateAsync(request, cancellationToken);

            if (!validationResult.IsValid)
            {
                var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
                return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
            }

            var normalizedEmail = request.Email?.Trim() ?? string.Empty;
            var user = await _userAccountRepository.GetByEmailAsync(normalizedEmail);

            if (user is null)
                return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

            var manualIdentity = user.AuthIdentities.FirstOrDefault(i =>
                i.Provider == AuthProvider.Manual &&
                (i.Email == null || i.Email.Equals(normalizedEmail, StringComparison.OrdinalIgnoreCase)));

            if (manualIdentity == null)
            {
                manualIdentity = user.AuthIdentities.FirstOrDefault(i => i.Provider == AuthProvider.Manual);
            }

            // Check password histories to avoid reusing any old passwords
            var isPreviousPassword = user.PasswordHistories.Any(h => _passwordHasher.VerifyPassword(h.PasswordHash, request.NewPassword));
            if (isPreviousPassword)
            {
                return Result<Guid>.Failure("You cannot reuse any of your previous passwords.", ErrorCode.BadRequest);
            }

            var resetTokenHash = Sha256Base64(request.ResetToken?.Trim() ?? string.Empty);
            var cleanResetToken = new string(request.ResetToken?.Where(char.IsDigit).ToArray() ?? Array.Empty<char>());
            var cleanResetTokenHash = !string.IsNullOrEmpty(cleanResetToken) ? Sha256Base64(cleanResetToken) : string.Empty;
            var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);

            var utcNow = DateTime.UtcNow;
            string? matchedHash = null;

            if (user.CanResetPasswordWithToken(resetTokenHash, utcNow))
            {
                matchedHash = resetTokenHash;
            }
            else if (!string.IsNullOrEmpty(cleanResetTokenHash) && user.CanResetPasswordWithToken(cleanResetTokenHash, utcNow))
            {
                matchedHash = cleanResetTokenHash;
            }

            if (matchedHash == null)
                return Result<Guid>.Failure("Invalid or expired reset token.", ErrorCode.Unauthorized);

            user.ResetPasswordWithToken(matchedHash, newPasswordHash, utcNow);
            if (manualIdentity != null)
            {
                manualIdentity.UpdatePasswordHash(newPasswordHash);
            }
            else
            {
                var newIdentity = AuthIdentity.CreateManual(user.Id, normalizedEmail, newPasswordHash, false);
                user.AuthIdentities.Add(newIdentity);
            }

            // Save new password to history
            user.PasswordHistories.Add(new PasswordHistory(user.Id, newPasswordHash));

            await _userAccountRepository.UpdateAsync(user);

            return Result<Guid>.Success(user.Id, "Password reset successful.");
        }
        catch (Exception ex)
        {
            return Result<Guid>.Failure($"An error occurred resetting your password: {ex.Message}", ErrorCode.BadRequest);
        }
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
