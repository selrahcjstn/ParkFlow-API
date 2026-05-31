using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Auth.Commands.UnlinkIdentity;

public class UnlinkIdentityCommandHandler : IRequestHandler<UnlinkIdentityCommand, Result<bool>>
{
    private readonly IAuthIdentityRepository _authIdentityRepository;
    private readonly IValidator<UnlinkIdentityCommand> _validator;

    public UnlinkIdentityCommandHandler(
        IAuthIdentityRepository authIdentityRepository,
        IValidator<UnlinkIdentityCommand> validator)
    {
        _authIdentityRepository = authIdentityRepository;
        _validator = validator;
    }

    public async Task<Result<bool>> Handle(UnlinkIdentityCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<bool>.Failure(false, errors, ErrorCode.BadRequest);
        }

        var identitiesList = (await _authIdentityRepository.GetByAccountIdAsync(request.UserId)).ToList();
        
        var targetIdentity = identitiesList.FirstOrDefault(i => i.Provider == request.Provider);
        if (targetIdentity == null)
        {
            return Result<bool>.Failure(false, "The specified authentication method is not linked to this account.", ErrorCode.NotFound);
        }

        if (identitiesList.Count <= 1)
        {
            return Result<bool>.Failure(false, "Cannot unlink the only authentication provider remaining on this account to prevent lockouts.", ErrorCode.Conflict);
        }

        await _authIdentityRepository.DeleteAsync(targetIdentity);

        return Result<bool>.Success(true, $"{request.Provider} identity unlinked successfully.");
    }
}
