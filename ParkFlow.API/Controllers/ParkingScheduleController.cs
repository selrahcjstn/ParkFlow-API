using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Schedules.Command;
using ParkFlow.Application.Features.Schedules.DTOs;
using ParkFlow.Application.Features.Schedules.Queries.GetParkingSchedule;

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

    [HttpGet("user/{userId:guid}")]
    public async Task<ActionResult<Result<IEnumerable<ParkingScheduleResponseDto>>>> GetByUserId(Guid userId)
    {
        var result = await _mediator.Send(new GetParkingScheduleQuery(userId));

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }
}
