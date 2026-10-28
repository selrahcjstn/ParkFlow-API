using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Features.Visitors.Queries.GetPagedVisitors;
using ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup;
using ParkFlow.Application.Features.Visitors.Queries.GetVisitorDetail;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

[Route("api/visitors")]
[ApiController]
public class VisitorController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public VisitorController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpGet("lookup/{plateNumber}")]
    public async Task<ActionResult<Result<UnifiedPlateLookupDto>>> LookupPlate(string plateNumber)
    {
        var result = await _mediator.Send(new GetPlateLookupQuery(plateNumber));
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpPost("lookup/{plateNumber}/confirm")]
    public async Task<ActionResult<Result<UnifiedPlateLookupDto>>> ConfirmPlateLookup(string plateNumber)
    {
        return this.ToActionResult(await _mediator.Send(new GetPlateLookupQuery(plateNumber, true)));
    }

    [Authorize(Policy = "GuardOnly")]
    [HttpPost("entry"), RequestSizeLimit(16 * 1024 * 1024)]
    public async Task<ActionResult<Result<VisitorEntryResponse>>> CreateVisitorEntry([FromForm] CreateVisitorEntryCommand command)
    {
        var callerUserId = _userContext.GetUserId();
        Guid? effectiveGuardId = callerUserId;

        var effectiveCommand = command with { GuardUserId = effectiveGuardId };
        var result = await _mediator.Send(effectiveCommand);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpPatch("exit")]
    public async Task<ActionResult<Result<VisitorExitResponse>>> ExitVisitorSessionPatch([FromBody] ExitVisitorSessionCommand command)
    {
        var callerUserId = _userContext.GetUserId();
        Guid? effectiveGuardId = callerUserId;

        var effectiveCommand = command with { GuardUserId = effectiveGuardId };
        var result = await _mediator.Send(effectiveCommand);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpPost("exit")]
    public async Task<ActionResult<Result<VisitorExitResponse>>> ExitVisitorSessionPost([FromBody] ExitVisitorSessionCommand command)
    {
        return await ExitVisitorSessionPatch(command);
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpGet]
    public async Task<ActionResult<Result<PagedVisitorsResponse>>> GetPagedVisitors(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] bool? onlyInside = null)
    {
        var result = await _mediator.Send(new GetPagedVisitorsQuery(page, pageSize, search, onlyInside));
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "GuardOrAdmin")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<VisitorDetailDto>>> GetVisitorDetail(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] Guid? visitorVehicleId = null)
    {
        var result = await _mediator.Send(new GetVisitorDetailQuery(id, page, pageSize, visitorVehicleId));
        return this.ToActionResult(result);
    }
}
