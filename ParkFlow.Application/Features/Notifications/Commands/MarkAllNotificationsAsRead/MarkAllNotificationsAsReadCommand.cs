using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Notifications.Commands.MarkAllNotificationsAsRead;

public record MarkAllNotificationsAsReadCommand(Guid UserId) : IRequest<Result<bool>>;

public class MarkAllNotificationsAsReadCommandHandler : IRequestHandler<MarkAllNotificationsAsReadCommand, Result<bool>>
{
    private readonly IUserNotificationRepository _repository;

    public MarkAllNotificationsAsReadCommandHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<bool>> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<bool>.Failure(false, "User ID is required.", ErrorCode.BadRequest);
        }

        await _repository.MarkAllAsReadForUserAsync(request.UserId);
        return Result<bool>.Success(true, "All notifications marked as read.");
    }
}
