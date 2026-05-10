using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;
using ParkFlow.Application.Features.Profiles.Commands;
using ParkFlow.Application.Features.Profiles.Queries;

namespace ParkFlow.API.Controllers;

[Route("api/profiles")]
[ApiController]
public class UserProfileController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserProfileController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<Result<Guid>>> Create(CreateUserProfileCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode == ErrorCode.Conflict
            ? Conflict(result)
            : BadRequest(result);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<Result<UserProfileDto>>> GetByUserId(Guid userId)
    {
        var result = await _mediator.Send(new GetUserProfileByUserIdQuery(userId));

        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode == ErrorCode.NotFound
            ? NotFound(result)
            : BadRequest(result);
    }

    [HttpPatch("{userId:guid}")]
    public async Task<ActionResult<Result<Guid>>> Update(Guid userId, UpdateUserProfileRequest request)
    {
        var command = new UpdateUserProfileCommand(
            userId,
            request.FirstName,
            request.LastName,
            request.ProfilePictureUrl);

        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result);

        return result.ErrorCode == ErrorCode.NotFound
            ? NotFound(result)
            : BadRequest(result);
    }
}
