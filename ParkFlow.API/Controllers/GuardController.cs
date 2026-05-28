using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.RegisterGuard.Commands.CreateGuardAccount;

namespace ParkFlow.API.Controllers;

[Route("api/guards")]
[ApiController]
public class GuardController : ControllerBase
{
    private readonly IMediator _mediator;

    public GuardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Creates a new guard account with login credentials.
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create([FromBody] CreateGuardAccountCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}