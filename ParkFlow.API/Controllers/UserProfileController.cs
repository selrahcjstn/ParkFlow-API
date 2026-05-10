using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Profiles.Commands;

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
}
