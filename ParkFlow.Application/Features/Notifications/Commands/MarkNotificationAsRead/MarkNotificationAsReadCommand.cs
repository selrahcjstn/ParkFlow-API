using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Notifications.Commands.MarkNotificationAsRead;

public record MarkNotificationAsReadCommand(Guid NotificationId, Guid UserId) : IRequest<Result<bool>>;

public class MarkNotificationAsReadCommandHandler : IRequestHandler<MarkNotificationAsReadCommand, Result<bool>>
{
    private readonly IUserNotificationRepository _repository;

    public MarkNotificationAsReadCommandHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
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

        notification.MarkAsRead();
        await _repository.UpdateAsync(notification);

        return Result<bool>.Success(true, "Notification marked as read.");
    }
}
