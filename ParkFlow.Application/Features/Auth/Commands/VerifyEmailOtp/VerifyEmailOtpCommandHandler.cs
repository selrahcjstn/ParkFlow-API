using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.VerifyEmailOtp;

public class VerifyEmailOtpCommandHandler : IRequestHandler<VerifyEmailOtpCommand, Result<bool>>
{
    private readonly IEmailOtpRepository _emailOtpRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IValidator<VerifyEmailOtpCommand> _validator;

    public VerifyEmailOtpCommandHandler(
        IEmailOtpRepository emailOtpRepository,
        IAuthIdentityRepository authIdentityRepository,
        IUserAccountRepository userAccountRepository,
        IValidator<VerifyEmailOtpCommand> validator)
    {
        _emailOtpRepository = emailOtpRepository;
        _authIdentityRepository = authIdentityRepository;
        _userAccountRepository = userAccountRepository;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(VerifyEmailOtpCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        var emailOtp = await _emailOtpRepository.GetLatestOtpByEmailAsync(request.Email);
        
        if (emailOtp == null)
        {
            return Result<bool>.Failure(false, "Verification code not found for this email.", ErrorCode.NotFound);
        }

        if (emailOtp.OtpCode != request.OtpCode)
        {
            return Result<bool>.Failure(false, "Invalid verification code.", ErrorCode.BadRequest);
        }

        if (emailOtp.IsUsed)
        {
            return Result<bool>.Failure(false, "Verification code has already been used.", ErrorCode.BadRequest);
        }

        if (emailOtp.ExpiresAt <= DateTime.UtcNow)
        {
            return Result<bool>.Failure(false, "Verification code has expired.", ErrorCode.Gone);
        }

        emailOtp.MarkAsUsed();
        await _emailOtpRepository.UpdateAsync(emailOtp);

        if (string.IsNullOrWhiteSpace(request.Purpose) || 
            request.Purpose.Equals("Verification", StringComparison.OrdinalIgnoreCase))
        {
            var authIdentity = await _authIdentityRepository.GetByEmailAsync(request.Email);
            if (authIdentity != null)
            {
                if (!authIdentity.IsVerified)
                {
                    authIdentity.MarkVerified();
                    await _authIdentityRepository.UpdateAsync(authIdentity);
                }

                // Also flip core UserAccount status to Verified
                var user = await _userAccountRepository.GetByIdAsync(authIdentity.UserAccountId);
                if (user != null && user.Status != AccountStatus.Active)
                {
                    user.Verify();
                    await _userAccountRepository.UpdateAsync(user);
                }
            }
            else
            {
                var user = await _userAccountRepository.GetByEmailAsync(request.Email);
                if (user != null && user.Status != AccountStatus.Active)
                {
                    user.Verify();
                    await _userAccountRepository.UpdateAsync(user);
                }
            }
        }

        return Result<bool>.Success(true, "Email verified successfully.");
    }
}
