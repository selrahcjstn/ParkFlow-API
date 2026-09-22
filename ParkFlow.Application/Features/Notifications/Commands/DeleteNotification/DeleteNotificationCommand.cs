using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Notifications.Commands.DeleteNotification;

public record DeleteNotificationCommand(Guid NotificationId, Guid UserId) : IRequest<Result<bool>>;

public class DeleteNotificationCommandHandler : IRequestHandler<DeleteNotificationCommand, Result<bool>>
{
    private readonly IUserNotificationRepository _repository;

    public DeleteNotificationCommandHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(DeleteNotificationCommand request, CancellationToken cancellationToken)
    {
        var notification = await _repository.GetByIdAsync(request.NotificationId);

        if (notification == null)
        {
            return Result<bool>.Failure(false, "Notification not found.", ErrorCode.NotFound);
        }

        if (notification.UserAccountId != request.UserId)
        {
            return Result<bool>.Failure(false, "Unauthorized access to notification.", ErrorCode.Unauthorized);
        }

        await _repository.DeleteAsync(notification);
        return Result<bool>.Success(true, "Notification deleted.");
    }
}
