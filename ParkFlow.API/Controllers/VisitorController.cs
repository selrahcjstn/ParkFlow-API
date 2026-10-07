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

    [Authorize]
    [HttpGet("lookup/{plateNumber}")]
    public async Task<ActionResult<Result<UnifiedPlateLookupDto>>> LookupPlate(string plateNumber)
    {
        var result = await _mediator.Send(new GetPlateLookupQuery(plateNumber));
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("entry")]
    public async Task<ActionResult<Result<VisitorEntryResponse>>> CreateVisitorEntry([FromBody] CreateVisitorEntryCommand command)
    {
        var callerUserId = _userContext.GetUserId();
        var effectiveGuardId = command.GuardUserId.HasValue && command.GuardUserId.Value != Guid.Empty
            ? command.GuardUserId.Value
            : (callerUserId != Guid.Empty ? callerUserId : (Guid?)null);

        var effectiveCommand = command with { GuardUserId = effectiveGuardId };
        var result = await _mediator.Send(effectiveCommand);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPatch("exit")]
    public async Task<ActionResult<Result<VisitorExitResponse>>> ExitVisitorSessionPatch([FromBody] ExitVisitorSessionCommand command)
    {
        var callerUserId = _userContext.GetUserId();
        var effectiveGuardId = command.GuardUserId.HasValue && command.GuardUserId.Value != Guid.Empty
            ? command.GuardUserId.Value
            : (callerUserId != Guid.Empty ? callerUserId : (Guid?)null);

        var effectiveCommand = command with { GuardUserId = effectiveGuardId };
        var result = await _mediator.Send(effectiveCommand);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("exit")]
    public async Task<ActionResult<Result<VisitorExitResponse>>> ExitVisitorSessionPost([FromBody] ExitVisitorSessionCommand command)
    {
        return await ExitVisitorSessionPatch(command);
    }

    [Authorize]
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

    [Authorize]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Result<VisitorDetailDto>>> GetVisitorDetail(Guid id)
    {
        var result = await _mediator.Send(new GetVisitorDetailQuery(id));
        return this.ToActionResult(result);
    }
}
