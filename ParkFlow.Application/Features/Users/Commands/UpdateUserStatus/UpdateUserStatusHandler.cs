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
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    private readonly ICacheService? _cacheService;

    public UpdateUserStatusHandler(
        IUserAccountRepository userAccountRepository,
        ISignalRNotificationSender? signalRNotificationSender = null,
        ICacheService? cacheService = null)
    {
        _userAccountRepository = userAccountRepository;
        _signalRNotificationSender = signalRNotificationSender;
        _cacheService = cacheService;
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

        if (_signalRNotificationSender != null)
        {
            if (newStatus == AccountStatus.Suspended)
            {
                await _signalRNotificationSender.SendToUserAsync(user.Id.ToString(), "AccountSuspended", new
                {
                    userId = user.Id,
                    message = "Your account has been suspended by the administrator."
                });
            }
            else
            {
                await _signalRNotificationSender.SendToUserAsync(user.Id.ToString(), "VerificationStatusChanged", new
                {
                    userId = user.Id,
                    type = "status_changed",
                    status = newStatus.ToString()
                });
            }
        }

        if (_cacheService != null)
        {
            await _cacheService.RemoveByPrefixAsync(CacheKeys.UserPrefix, cancellationToken);
            await _cacheService.RemoveByPrefixAsync(CacheKeys.DashboardPrefix, cancellationToken);
        }

        return Result<Guid>.Success(user.Id, "User status updated successfully.");
    }
}
