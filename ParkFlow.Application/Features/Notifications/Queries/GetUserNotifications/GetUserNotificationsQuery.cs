using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Notifications.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Notifications.Queries.GetUserNotifications;

public record GetUserNotificationsQuery(Guid UserId, int Limit = 50) : IRequest<Result<IEnumerable<UserNotificationDto>>>;

public class GetUserNotificationsQueryHandler : IRequestHandler<GetUserNotificationsQuery, Result<IEnumerable<UserNotificationDto>>>
{
    private readonly IUserNotificationRepository _repository;

    public GetUserNotificationsQueryHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IEnumerable<UserNotificationDto>>> Handle(GetUserNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<IEnumerable<UserNotificationDto>>.Failure("User ID is required.", ErrorCode.BadRequest);
        }

        var notifications = await _repository.GetByUserIdAsync(request.UserId, request.Limit);
        var dtos = notifications.Select(UserNotificationDto.FromEntity);

        return Result<IEnumerable<UserNotificationDto>>.Success(dtos, "Notifications retrieved successfully.");
    }
}
