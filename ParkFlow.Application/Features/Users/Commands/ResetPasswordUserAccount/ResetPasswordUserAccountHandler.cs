using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

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
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userAccountRepository.GetByEmailAsync(request.Email);

        if (user is null)
            return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

        if (user.AuthProvider != AuthProvider.Manual || string.IsNullOrWhiteSpace(user.PasswordHash))
            return Result<Guid>.Failure("Password reset is only available for manual accounts.", ErrorCode.BadRequest);

        var resetTokenHash = Sha256Base64(request.ResetToken);
        var newPasswordHash = _passwordHasher.HashPassword(request.NewPassword);

        var utcNow = DateTime.UtcNow;
        if (!user.CanResetPasswordWithToken(resetTokenHash, utcNow))
            return Result<Guid>.Failure("Invalid or expired reset token.", ErrorCode.Unauthorized);

        user.ResetPasswordWithToken(resetTokenHash, newPasswordHash, utcNow);
        await _userAccountRepository.UpdateAsync(user);

        return Result<Guid>.Success(user.Id, "Password reset successful.");
    }

    private static string Sha256Base64(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
