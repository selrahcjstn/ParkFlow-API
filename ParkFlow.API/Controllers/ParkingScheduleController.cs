using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Schedules.Command;
using ParkFlow.Application.Features.Schedules.DTOs;
using ParkFlow.Application.Features.Schedules.Queries.GetParkingSchedule;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

[Route("api/parking-schedules")]
[ApiController]
public class ParkingScheduleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public ParkingScheduleController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<ActionResult<Result<Guid>>> Create(CreateParkingScheduleCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<Result<IEnumerable<ParkingScheduleResponseDto>>>> GetMySchedule()
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<IEnumerable<ParkingScheduleResponseDto>>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new GetParkingScheduleQuery(userId));

        if (result.IsSuccess)
            return Ok(result);

        return this.ToActionResult(result);
    }
}
