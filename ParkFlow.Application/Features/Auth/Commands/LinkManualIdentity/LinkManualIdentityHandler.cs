using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace ParkFlow.Application.Features.Auth.Commands.LinkManualIdentity;

public class LinkManualIdentityHandler : IRequestHandler<LinkManualIdentityCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IEmailOtpRepository _emailOtpRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<LinkManualIdentityCommand> _validator;

    public LinkManualIdentityHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IEmailOtpRepository emailOtpRepository,
        IPasswordHasher passwordHasher,
        IValidator<LinkManualIdentityCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _emailOtpRepository = emailOtpRepository;
        _passwordHasher = passwordHasher;
        _validator = validator;
    }

    public async Task<Result<Guid>> Handle(LinkManualIdentityCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<Guid>.Failure(errors, ErrorCode.BadRequest);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

        var existingIdentity = await _authIdentityRepository.GetByEmailAsync(request.Email);
        if (existingIdentity != null)
        {
            var message = existingIdentity.UserAccountId == request.UserId
                ? "Email is already linked to this account."
                : "Email is already linked to another account.";

            return Result<Guid>.Failure(message, ErrorCode.Conflict);
        }

        var hashedPassword = _passwordHasher.HashPassword(request.Password);
        var identity = AuthIdentity.CreateManual(user.Id, request.Email, hashedPassword, isPrimary: !user.AuthIdentities.Any());
        var latestOtp = await _emailOtpRepository.GetLatestOtpByEmailAsync(request.Email);
        if (latestOtp?.IsUsed == true)
        {
            identity.MarkVerified();
            if (user.Status != ParkFlow.Domain.Enums.AccountStatus.Verified)
            {
                user.Verify();
            }
        }

        user.PasswordHistories.Add(new PasswordHistory(user.Id, hashedPassword));
        await _userAccountRepository.UpdateAsync(user);
        await _authIdentityRepository.AddAsync(identity);

        return Result<Guid>.Success(identity.Id, "Manual login linked successfully.");
    }
}
