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
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IValidator<UpdateManualIdentityCommand> _validator;

    public UpdateManualIdentityCommandHandler(
        IAuthIdentityRepository authIdentityRepository,
        IValidator<UpdateManualIdentityCommand> validator)
    {
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
        if (existingIdentity != null && existingIdentity.Id != manualIdentity.Id)
        {
            var message = existingIdentity.UserAccountId == request.UserId
                ? "Email is already linked to this account."
                : "Email is already linked to another account.";

            return Result<bool>.Failure(false, message, ErrorCode.Conflict);
        }

        // Update email on Manual Identity
        manualIdentity.UpdateEmail(request.NewEmail);
        await _authIdentityRepository.UpdateAsync(manualIdentity);

        return Result<bool>.Success(true, "Manual link email updated successfully.");
    }
}
