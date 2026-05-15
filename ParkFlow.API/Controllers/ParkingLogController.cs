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

    [HttpPost("entry")]
    public async Task<ActionResult<Result<Guid>>> LogEntry(Guid vehicleId, Guid guardId, DateTime? entryTime = null)
    {
        var command = new CreateParkingLogCommand(
            vehicleId,
            guardId,
            entryTime ?? DateTime.UtcNow,
            null,
            ParkingStatus.Parked);

        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("exit")]
    public async Task<ActionResult<Result<Guid>>> LogExit(Guid vehicleId, Guid guardId, DateTime? exitTime = null)
    {
        var command = new CreateParkingLogCommand(
            vehicleId,
            guardId,
            DateTime.UtcNow,
            exitTime ?? DateTime.UtcNow,
            ParkingStatus.Exited);

        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
