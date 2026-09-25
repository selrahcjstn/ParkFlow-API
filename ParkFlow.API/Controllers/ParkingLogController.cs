using MediatR;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.CreateManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitParkingLog;
using ParkFlow.Application.Features.ParkingLogs.Commands.ExitManualParkingLog;
using ParkFlow.Application.Features.ParkingLogs.DTOs;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSessionCount;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveParkingSession;
using ParkFlow.Application.Features.ParkingLogs.Queries.GetActiveSessionByVehicleId;
using ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;

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

    // VERIFY STUDENT SCAN
    [HttpPost("verify-student-scan")]
    public async Task<ActionResult<Result<VerifyStudentScanResponse>>> VerifyStudentScan([FromBody] VerifyStudentScanQuery query)
    {
        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }

    // ENTRY
    [HttpPost("entry")]
    public async Task<ActionResult<Result<CreateParkingLogResponse>>> LogEntry([FromBody] CreateParkingLogCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    // MANUAL ENTRY
    [HttpPost("manual-entry")]
    public async Task<ActionResult<Result<CreateParkingLogResponse>>> LogManualEntry([FromBody] CreateManualParkingLogCommand command)
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

    // MANUAL EXIT
    [HttpPatch("manual-exit")]
    public async Task<ActionResult<Result<ExitParkingLogResponse>>> LogManualExit([FromBody] ExitManualParkingLogCommand command)
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

    [HttpGet("session-count")]
    public async Task<ActionResult<Result<SessionCountResponse>>> GetSessionCount(
        [FromQuery] int parkingCapacity = 100)
    {
        var result = await _mediator.Send(
            new GetSessionCountQuery(parkingCapacity)
        );

        return this.ToActionResult(result);
    }

    [HttpGet("active/vehicle/{vehicleId:guid}")]
    public async Task<ActionResult<Result<ActiveParkingSessionResponse>>> GetActiveSessionByVehicleId(Guid vehicleId)
    {
        var result = await _mediator.Send(new GetActiveSessionByVehicleIdQuery(vehicleId));
        return this.ToActionResult(result);
    }
}