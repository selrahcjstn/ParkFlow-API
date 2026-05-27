using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Users.Commands.RegisterUserAggregate;

namespace ParkFlow.API.Controllers;

[Route("api/register")]
[ApiController]
public class RegisterController : ControllerBase
{
    private readonly IMediator _mediator;

    public RegisterController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Registers a new user with profile, student/personnel info, vehicle, and COR submission in a single request.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Result<RegisterResultDto>>> Register([FromBody] RegisterUserAggregateCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
