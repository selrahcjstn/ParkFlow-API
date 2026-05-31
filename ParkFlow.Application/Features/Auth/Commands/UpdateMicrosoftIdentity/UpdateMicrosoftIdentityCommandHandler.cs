using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.UpdateMicrosoftIdentity;

public class UpdateMicrosoftIdentityCommandHandler : IRequestHandler<UpdateMicrosoftIdentityCommand, Result<bool>>
{
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IValidator<UpdateMicrosoftIdentityCommand> _validator;

    public UpdateMicrosoftIdentityCommandHandler(
        IAuthIdentityRepository authIdentityRepository,
        IValidator<UpdateMicrosoftIdentityCommand> validator)
    {
        _authIdentityRepository = authIdentityRepository;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(UpdateMicrosoftIdentityCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        var identities = (await _authIdentityRepository.GetByAccountIdAsync(request.UserId)).ToList();
        var microsoftIdentity = identities.FirstOrDefault(i => i.Provider == AuthProvider.Microsoft);
        
        if (microsoftIdentity == null)
        {
            return Result<bool>.Failure(false, "Microsoft login is not linked to this account.", ErrorCode.NotFound);
        }

        // Check if new external provider ID is already linked to another account
        var existingProvider = await _authIdentityRepository.GetByProviderIdAsync(AuthProvider.Microsoft, request.NewExternalProviderId);
        if (existingProvider != null && existingProvider.UserAccountId != request.UserId)
        {
            return Result<bool>.Failure(false, "Microsoft account is already linked to another user.", ErrorCode.Conflict);
        }

        // Check if new email is already linked to another account
        var existingEmail = await _authIdentityRepository.GetByEmailAsync(request.NewEmail);
        if (existingEmail != null && existingEmail.Id != microsoftIdentity.Id)
        {
            var message = existingEmail.UserAccountId == request.UserId
                ? "Email is already linked to this account."
                : "Email is already linked to another account.";

            return Result<bool>.Failure(false, message, ErrorCode.Conflict);
        }

        // Update email and provider ID on Microsoft Identity
        microsoftIdentity.UpdateEmail(request.NewEmail);
        microsoftIdentity.UpdateProviderId(request.NewExternalProviderId);
        await _authIdentityRepository.UpdateAsync(microsoftIdentity);

        return Result<bool>.Success(true, "Microsoft link updated successfully.");
    }
}
