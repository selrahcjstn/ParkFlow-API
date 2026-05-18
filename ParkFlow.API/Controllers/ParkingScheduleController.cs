using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Schedules.Command;

namespace ParkFlow.API.Controllers;

[Route("api/parking-schedules")]
[ApiController]
public class ParkingScheduleController : ControllerBase
{
    private readonly IMediator _mediator;

    public ParkingScheduleController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create(CreateParkingScheduleCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }
}
