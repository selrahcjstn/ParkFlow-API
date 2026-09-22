using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Notifications.Commands.DeleteNotification;
using ParkFlow.Application.Features.Notifications.Commands.MarkAllNotificationsAsRead;
using ParkFlow.Application.Features.Notifications.Commands.MarkNotificationAsRead;
using ParkFlow.Application.Features.Notifications.DTOs;
using ParkFlow.Application.Features.Notifications.Queries.GetUnreadNotificationCount;
using ParkFlow.Application.Features.Notifications.Queries.GetUserNotifications;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.API.Controllers;

[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public NotificationsController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// Gets all persistent notifications for the currently authenticated user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<Result<IEnumerable<UserNotificationDto>>>> GetMyNotifications([FromQuery] int limit = 50)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<IEnumerable<UserNotificationDto>>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new GetUserNotificationsQuery(userId, limit));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Gets the count of unread notifications for the currently authenticated user.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<Result<int>>> GetUnreadCount()
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<int>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new GetUnreadNotificationCountQuery(userId));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Marks a single notification as read for the currently authenticated user.
    /// </summary>
    [HttpPut("{id:guid}/read")]
    public async Task<ActionResult<Result<bool>>> MarkAsRead([FromRoute] Guid id)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new MarkNotificationAsReadCommand(id, userId));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Marks all notifications as read for the currently authenticated user.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<ActionResult<Result<bool>>> MarkAllAsRead()
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new MarkAllNotificationsAsReadCommand(userId));
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Deletes a notification record for the currently authenticated user.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Result<bool>>> DeleteNotification([FromRoute] Guid id)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new DeleteNotificationCommand(id, userId));
        return this.ToActionResult(result);
    }
}
