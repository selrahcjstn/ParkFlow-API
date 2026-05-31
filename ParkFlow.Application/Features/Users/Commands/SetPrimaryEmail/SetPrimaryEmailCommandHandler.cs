using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Commands.SetPrimaryEmail;

public class SetPrimaryEmailCommandHandler : IRequestHandler<SetPrimaryEmailCommand, Result<bool>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IValidator<SetPrimaryEmailCommand> _validator;

    public SetPrimaryEmailCommandHandler(
        IUserAccountRepository userAccountRepository,
        IAuthIdentityRepository authIdentityRepository,
        IValidator<SetPrimaryEmailCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
        _authIdentityRepository = authIdentityRepository;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(SetPrimaryEmailCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            return Result<bool>.Failure(false, "User account not found.", ErrorCode.NotFound);
        }

        // Check if email is already taken by another user account
        var isEmailTaken = await _userAccountRepository.EmailExistsAsync(request.Email, user.Id);
        if (isEmailTaken)
        {
            return Result<bool>.Failure(false, "Email is already taken by another account.", ErrorCode.Conflict);
        }

        // Update email on Manual AuthIdentity if it exists
        var identities = (await _authIdentityRepository.GetByAccountIdAsync(user.Id)).ToList();
        var manualIdentity = identities.FirstOrDefault(i => i.Provider == AuthProvider.Manual);
        if (manualIdentity != null)
        {
            manualIdentity.UpdateEmail(request.Email);
            await _authIdentityRepository.UpdateAsync(manualIdentity);
        }

        // Update primary email on core UserAccount
        user.UpdateEmail(request.Email);
        await _userAccountRepository.UpdateAsync(user);

        return Result<bool>.Success(true, "Primary email updated successfully.");
    }
}
