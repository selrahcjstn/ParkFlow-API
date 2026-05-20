using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;

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
    public async Task<ActionResult<Result<CreateParkingLogResponse>>> LogEntry([FromBody] CreateParkingLogCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    // EXIT
    [HttpPatch("exit")]
    public async Task<ActionResult<Result<ExitParkingLogResponse>>> LogExit([FromBody] ExitParkingLogCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
    [HttpGet("active-sessions")]
    public async Task<ActionResult<Result<IEnumerable<GetActiveParkingSessionResponse>>>> GetActiveSessions(
        [FromQuery] int parkingCapacity = 100)
    {
        var result = await _mediator.Send(
            new GetActiveParkingSessionQuery(parkingCapacity)
        );

        return this.ToActionResult(result);
    }
}