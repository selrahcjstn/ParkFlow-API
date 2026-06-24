using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.History.DTOs;
using ParkFlow.Application.Features.History.Queries;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

[Route("api/parking-history")]
[ApiController]
[Authorize]
public class ParkingHistoryController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public ParkingHistoryController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpGet("page/{pageNumber:int}/{pageSize:int}")]
    public async Task<ActionResult<Result<PagedParkingHistoryResponse>>> GetMyHistory(
        int pageNumber,
        int pageSize)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<PagedParkingHistoryResponse>.Failure("User not identified.", ErrorCode.Unauthorized));

        // Validation constraints
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 45) pageSize = 45;

        var result = await _mediator.Send(new GetParkingHistoryQuery(userId, pageNumber, pageSize));

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }

    [HttpGet("all/page/{pageNumber:int}/{pageSize:int}")]
    public async Task<ActionResult<Result<PagedParkingHistoryResponse>>> GetAllHistory(
        int pageNumber,
        int pageSize)
    {
        // Validation constraints
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 15;
        if (pageSize > 45) pageSize = 45;

        var result = await _mediator.Send(new GetParkingHistoryQuery(Guid.Empty, pageNumber, pageSize));

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }
}
