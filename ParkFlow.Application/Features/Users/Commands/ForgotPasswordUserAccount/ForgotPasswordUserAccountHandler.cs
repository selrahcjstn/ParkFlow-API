using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.Commands.ForgotPasswordUserAccount;

public class ForgotPasswordUserAccountHandler
    : IRequestHandler<ForgotPasswordUserAccountCommand, Result<string>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<ForgotPasswordUserAccountCommand> _validator;

    public ForgotPasswordUserAccountHandler(
        IUserAccountRepository userAccountRepository,
        IValidator<ForgotPasswordUserAccountCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
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

        if (user.AuthProvider != AuthProvider.Manual || string.IsNullOrWhiteSpace(user.PasswordHash))
            return Result<string>.Failure("Password reset is only available for manual accounts.", ErrorCode.BadRequest);

        var resetToken = GenerateResetToken();
        var resetTokenHash = Sha256Base64(resetToken);
        var expiresAt = DateTime.UtcNow.AddHours(1);

        user.SetPasswordResetToken(resetTokenHash, expiresAt);
        await _userAccountRepository.UpdateAsync(user);

        return Result<string>.Success(resetToken, "Password reset token generated.");
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
