using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Notifications.Queries.GetUnreadNotificationCount;

public record GetUnreadNotificationCountQuery(Guid UserId) : IRequest<Result<int>>;

public class GetUnreadNotificationCountQueryHandler : IRequestHandler<GetUnreadNotificationCountQuery, Result<int>>
{
    private readonly IUserNotificationRepository _repository;

    public GetUnreadNotificationCountQueryHandler(IUserNotificationRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<int>> Handle(GetUnreadNotificationCountQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return Result<int>.Failure("User ID is required.", ErrorCode.BadRequest);
        }

        var count = await _repository.GetUnreadCountAsync(request.UserId);
        return Result<int>.Success(count, "Unread count retrieved successfully.");
    }
}
