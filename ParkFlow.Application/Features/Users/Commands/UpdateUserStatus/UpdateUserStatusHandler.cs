using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserStatus;

public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, Result<Guid>>
{
    private readonly IUserAccountRepository _userAccountRepository;

    public UpdateUserStatusHandler(IUserAccountRepository userAccountRepository)
    {
        _userAccountRepository = userAccountRepository;
    }

    public async Task<Result<Guid>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            return Result<Guid>.Failure("Status is required.", ErrorCode.BadRequest);

        if (!Enum.TryParse<AccountStatus>(request.Status, true, out var newStatus))
            return Result<Guid>.Failure($"Invalid status: {request.Status}. Allowed: Suspended, PendingVerification, Active.", ErrorCode.BadRequest);

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<Guid>.Failure("User account not found.", ErrorCode.NotFound);

        user.UpdateStatus(newStatus);
        await _userAccountRepository.UpdateAsync(user);

        return Result<Guid>.Success(user.Id, "User status updated successfully.");
    }
}
