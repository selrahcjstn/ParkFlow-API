using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetRecentParkingHistory;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetTodayParkingLogs;

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
    public async Task<ActionResult<Result<ParkFlow.Application.Features.ParkingLogs.DTOs.CreateParkingLogResponse>>> LogEntry([FromBody] CreateParkingLogCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // EXIT
    [HttpPatch("exit")]
    public async Task<ActionResult<Result<ExitParkingLogResponse>>> LogExit([FromBody] ExitParkingLogCommand command)
    {
        var result = await _mediator.Send(command);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("today")]
    public async Task<ActionResult<Result<ParkingLogsTodayResponse>>> GetToday([FromQuery] int limit = 100)
    {
        var result = await _mediator.Send(new GetTodayParkingLogsQuery(limit));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<Result<IEnumerable<ParkFlow.Application.Features.ParkingLogs.DTOs.ParkingLogHistoryDto>>>> GetHistory([FromQuery] int limit = 20)
    {
        var result = await _mediator.Send(new GetRecentParkingHistoryQuery(limit));
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}