using MediatR;
using Microsoft.AspNetCore.Authorization;
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

using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

[Route("api/parking-logs")]
[ApiController]
public class ParkingLogController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext? _userContext;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly IAuthorizationService? _authorization;

    public ParkingLogController(IMediator mediator, IUserContext? userContext = null,
        IVehicleRepository? vehicleRepository = null, IAuthorizationService? authorization = null)
    {
        _mediator = mediator;
        _userContext = userContext;
        _vehicleRepository = vehicleRepository;
        _authorization = authorization;
    }

    [HttpPost("preview"), Microsoft.AspNetCore.Authorization.Authorize(Policy = "GuardOrAdmin")]
    public async Task<ActionResult<Result<ParkFlow.Application.Features.ParkingLogs.Queries.VehicleScanPreview>>> Preview(
        ParkFlow.Application.Features.ParkingLogs.Queries.PreviewVehicleScan query, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return this.ToActionResult(await _mediator.Send(query, ct));
    }

    // VERIFY STUDENT SCAN
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "GuardOrAdmin")]
    [HttpPost("verify-student-scan")]
    public async Task<ActionResult<Result<VerifyStudentScanResponse>>> VerifyStudentScan([FromBody] VerifyStudentScanQuery query)
    {
        var result = await _mediator.Send(query);
        return this.ToActionResult(result);
    }

    // ENTRY
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "GuardOrAdmin")]
    [HttpPost("entry")]
    public async Task<ActionResult<Result<CreateParkingLogResponse>>> LogEntry([FromBody] CreateParkingLogCommand command)
    {
        command = command with { UserId = _userContext?.GetUserId() ?? Guid.Empty };
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    // MANUAL ENTRY
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "GuardOrAdmin")]
    [HttpPost("manual-entry")]
    public async Task<ActionResult<Result<CreateParkingLogResponse>>> LogManualEntry([FromBody] CreateManualParkingLogCommand command)
    {
        command = command with { UserId = _userContext?.GetUserId() ?? Guid.Empty };
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    // EXIT
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "GuardOrAdmin")]
    [HttpPatch("exit")]
    public async Task<ActionResult<Result<ExitParkingLogResponse>>> LogExit([FromBody] ExitParkingLogCommand command)
    {
        command = command with { UserId = _userContext?.GetUserId() ?? Guid.Empty };
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    // MANUAL EXIT
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "GuardOrAdmin")]
    [HttpPatch("manual-exit")]
    public async Task<ActionResult<Result<ExitParkingLogResponse>>> LogManualExit([FromBody] ExitManualParkingLogCommand command)
    {
        var callerUserId = _userContext?.GetUserId() ?? Guid.Empty;
        Guid? effectiveUserId = callerUserId;

        var effectiveCommand = command with { UserId = effectiveUserId };
        var result = await _mediator.Send(effectiveCommand);
        return this.ToActionResult(result);
    }
    [Authorize(Policy = "GuardOrAdmin")]
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

    [Authorize]
    [HttpGet("active/vehicle/{vehicleId:guid}")]
    public async Task<ActionResult<Result<ActiveParkingSessionResponse>>> GetActiveSessionByVehicleId(Guid vehicleId)
    {
        var callerId = _userContext?.GetUserId() ?? Guid.Empty;
        if (User.Identity?.IsAuthenticated != true || callerId == Guid.Empty) return Unauthorized();
        var canReadFleet = _authorization != null && (await _authorization.AuthorizeAsync(User, null, "GuardOrAdmin")).Succeeded;
        if (!canReadFleet)
        {
            var vehicle = _vehicleRepository == null ? null : await _vehicleRepository.GetByIdAsync(vehicleId);
            if (vehicle == null || vehicle.OwnerId != callerId) return NotFound();
        }
        var result = await _mediator.Send(new GetActiveSessionByVehicleIdQuery(vehicleId));
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpGet("history/page/{pageNumber:int}/{pageSize:int}")]
    public async Task<ActionResult<Result<ParkFlow.Application.Features.History.DTOs.PagedParkingHistoryResponse>>> GetParkingHistory(
        int pageNumber,
        int pageSize)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 1000) pageSize = 1000;

        var result = await _mediator.Send(new ParkFlow.Application.Features.History.Queries.GetParkingHistoryQuery(Guid.Empty, pageNumber, pageSize));
        return this.ToActionResult(result);
    }
}