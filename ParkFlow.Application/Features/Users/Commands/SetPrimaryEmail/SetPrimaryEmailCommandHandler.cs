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
    private readonly IValidator<SetPrimaryEmailCommand> _validator;

    public SetPrimaryEmailCommandHandler(
        IUserAccountRepository userAccountRepository,
        IValidator<SetPrimaryEmailCommand> validator)
    {
        _userAccountRepository = userAccountRepository;
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

        var identities = user.AuthIdentities.ToList();

        var targetIdentity = identities.FirstOrDefault(i =>
            i.Email != null && i.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase));

        if (targetIdentity == null)
        {
            return Result<bool>.Failure(false, "Email is not linked to this account.", ErrorCode.NotFound);
        }

        if (targetIdentity.IsPrimary)
        {
            return Result<bool>.Failure(false, "Email is already the primary email.", ErrorCode.BadRequest);
        }

        // Step 1: Set IsPrimary = false for all other primary identities and save
        var otherPrimaries = identities.Where(i => i.Id != targetIdentity.Id && i.IsPrimary).ToList();
        if (otherPrimaries.Any())
        {
            foreach (var other in otherPrimaries)
            {
                other.SetPrimary(false);
            }
            await _userAccountRepository.UpdateAsync(user);
        }

        // Step 2: Now set the target identity as primary and save
        targetIdentity.MarkAsPrimary();
        await _userAccountRepository.UpdateAsync(user);

        return Result<bool>.Success(true, "Primary email updated successfully.");
    }
}
