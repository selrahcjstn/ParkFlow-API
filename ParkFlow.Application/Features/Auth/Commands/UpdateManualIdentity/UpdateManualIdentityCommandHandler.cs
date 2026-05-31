using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.UpdateManualIdentity;

public class UpdateManualIdentityCommandHandler : IRequestHandler<UpdateManualIdentityCommand, Result<bool>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IValidator<UpdateManualIdentityCommand> _validator;

    public UpdateManualIdentityCommandHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IValidator<UpdateManualIdentityCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(UpdateManualIdentityCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        var identities = (await _authIdentityRepository.GetByAccountIdAsync(request.UserId)).ToList();
        var manualIdentity = identities.FirstOrDefault(i => i.Provider == AuthProvider.Manual);
        
        if (manualIdentity == null)
        {
            return Result<bool>.Failure(false, "Manual login is not linked to this account.", ErrorCode.NotFound);
        }

        // Check if new email is already linked to another account
        var existingIdentity = await _authIdentityRepository.GetByEmailAsync(request.NewEmail);
        if (existingIdentity != null && existingIdentity.UserAccountId != request.UserId)
        {
            return Result<bool>.Failure(false, "Email is already linked to another account.", ErrorCode.Conflict);
        }

        // Update email on Manual Identity
        manualIdentity.UpdateEmail(request.NewEmail);
        await _authIdentityRepository.UpdateAsync(manualIdentity);

        // Also sync email on core UserAccount
        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user != null)
        {
            user.UpdateEmail(request.NewEmail);
            await _userAccountRepository.UpdateAsync(user);
        }

        return Result<bool>.Success(true, "Manual link email updated successfully.");
    }
}
