using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;

namespace ParkFlow.API.Controllers;

[Route("api/parking-logs")]
[ApiController]
public class ParkingLogController : ControllerBase
{
    private readonly IMediator _mediator;

    public ParkingLogController(IMediator mediator)
    {
        _mediator = mediator;
    }

    // ENTRY
    [HttpPost("entry")]
    public async Task<ActionResult<Result<Guid>>> LogEntry([FromBody] CreateParkingLogCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}