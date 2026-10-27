using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Features.Users.Commands.UpdateUserStatus;

public class UpdateUserStatusHandler : IRequestHandler<UpdateUserStatusCommand, Result<UpdateUserStatusResponse>>
{
    private readonly IUserAccountRepository _userAccountRepository;
    private readonly IVerificationStore _verificationStore;
    private readonly IMediator _mediator;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    private readonly ICacheService? _cacheService;

    public UpdateUserStatusHandler(
        IUserAccountRepository userAccountRepository,
        IVerificationStore verificationStore,
        IMediator mediator,
        ISignalRNotificationSender? signalRNotificationSender = null,
        ICacheService? cacheService = null)
    {
        _userAccountRepository = userAccountRepository;
        _verificationStore = verificationStore;
        _mediator = mediator;
        _signalRNotificationSender = signalRNotificationSender;
        _cacheService = cacheService;
    }

    public async Task<Result<UpdateUserStatusResponse>> Handle(UpdateUserStatusCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            return Result<UpdateUserStatusResponse>.Failure("Status is required.", ErrorCode.BadRequest);

        if (!Enum.TryParse<AccountStatus>(request.Status, true, out var newStatus) || !Enum.IsDefined(newStatus))
            return Result<UpdateUserStatusResponse>.Failure($"Invalid status: {request.Status}. Allowed: Suspended, PendingVerification, Active.", ErrorCode.BadRequest);

        var user = await _userAccountRepository.GetByIdAsync(request.UserId);
        if (user == null)
            return Result<UpdateUserStatusResponse>.Failure("User account not found.", ErrorCode.NotFound);

        if (newStatus == AccountStatus.Active && user.Status == AccountStatus.Suspended)
        {
            var restored = await _verificationStore.RestoreAccountAsync(user.Id, cancellationToken);
            if (restored == null)
                return Result<UpdateUserStatusResponse>.Failure("User account not found.", ErrorCode.NotFound);
            newStatus = restored.Value;
        }
        else if (user.UserProfile?.Personnel != null && user.Status != AccountStatus.Suspended && newStatus != AccountStatus.Suspended)
        {
            var approval = newStatus == AccountStatus.Active;
            var review = await _mediator.Send(new ParkFlow.Application.Features.Verification.ReviewVerificationSubject("Account", user.Id,
                approval ? "Approved" : "Rejected", approval ? "Account identity reviewed by an administrator." : "Account requires further administrative review.", request.ActorId), cancellationToken);
            if (!review.IsSuccess) return Result<UpdateUserStatusResponse>.Failure(review.Message, review.ErrorCode);
            newStatus = (await _userAccountRepository.GetByIdAsync(user.Id))!.Status;
        }
        else
        {
            if (newStatus == AccountStatus.Active && user.UserProfile?.Student != null && user.Status != AccountStatus.Active)
                return Result<UpdateUserStatusResponse>.Failure("Approve the student's vehicle documents from Vehicles before activating the account.", ErrorCode.BadRequest);
            user.UpdateStatus(newStatus);
            await _userAccountRepository.UpdateAsync(user);
        }

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

        return Result<UpdateUserStatusResponse>.Success(new(user.Id, newStatus.ToString()), "User status updated successfully.");
    }
}
