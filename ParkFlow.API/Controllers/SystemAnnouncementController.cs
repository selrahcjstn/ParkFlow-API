using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.SystemAnnouncements.Commands.UpdateSystemAnnouncement;
using ParkFlow.Application.Features.SystemAnnouncements.Queries.GetActiveSystemAnnouncement;
using ParkFlow.Application.Features.SystemAnnouncements.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

public record UpdateSystemAnnouncementRequest(string Message, string IconType = "caution", bool IsActive = true);

[Route("api/system-announcement")]
[ApiController]
public class SystemAnnouncementController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public SystemAnnouncementController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    /// <summary>
    /// Returns the currently active system-wide message banner (if any).
    /// </summary>
    [HttpGet("active")]
    [AllowAnonymous]
    public async Task<ActionResult<Result<SystemAnnouncementDto?>>> GetActiveAnnouncement()
    {
        var result = await _mediator.Send(new GetActiveSystemAnnouncementQuery());
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Creates or updates the active system-wide message banner (SuperAdmin required).
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<SystemAnnouncementDto>>> UpdateAnnouncement([FromBody] UpdateSystemAnnouncementRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<SystemAnnouncementDto>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateSystemAnnouncementCommand(
            userId,
            request.Message,
            request.IconType,
            request.IsActive
        );

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Deactivates/clears the system-wide message banner (SuperAdmin required).
    /// </summary>
    [HttpDelete]
    [Authorize]
    public async Task<ActionResult<Result<SystemAnnouncementDto>>> DeactivateAnnouncement()
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<SystemAnnouncementDto>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateSystemAnnouncementCommand(
            userId,
            "No active system message.",
            "info",
            false
        );

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
