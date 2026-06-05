using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.DTOs;
using ParkFlow.Application.Features.Profiles.Queries;
using ParkFlow.Application.Features.Profiles.Commands.CreateUserProfile;
using ParkFlow.Application.Features.Profiles.Commands.UpdateUserProfile;

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

    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create(CreateUserProfileCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<Result<UserProfileDto>>> GetByUserId()
    {
        var result = await _mediator.Send(new GetMyProfileQuery());
        return this.ToActionResult(result);
    }

    [HttpPatch("{userId:guid}")]
    public async Task<ActionResult<Result<Guid>>> Update(Guid userId, UpdateUserProfileRequest request)
    {
        var command = new UpdateUserProfileCommand(
            userId,
            request.FirstName,
            request.LastName,
            request.MiddleName,
            request.ProfilePictureUrl);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
