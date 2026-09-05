using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Reservations.Commands.ApproveReservation;
using ParkFlow.Application.Features.Reservations.Commands.CancelReservation;
using ParkFlow.Application.Features.Reservations.Commands.CreateReservation;
using ParkFlow.Application.Features.Reservations.Commands.RejectReservation;
using ParkFlow.Application.Features.Reservations.DTOs;
using ParkFlow.Application.Features.Reservations.Queries.GetAllReservations;
using ParkFlow.Application.Features.Reservations.Queries.GetMyReservations;
using ParkFlow.Application.Features.Reservations.Queries.GetReservationById;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.API.Controllers;

[Route("api/parking-reservations")]
[ApiController]
public class ParkingReservationController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public ParkingReservationController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    public record CreateReservationRequest(
        DateTime ReservationDate,
        string StartTime,
        string EndTime,
        string Reason);

    public record ReviewReservationRequest(string? Notes);

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Result<ParkingReservationDto>>> Create([FromBody] CreateReservationRequest request)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<ParkingReservationDto>.Failure("User not identified.", ErrorCode.Unauthorized));

        if (!TimeSpan.TryParse(request.StartTime, out var startTime))
        {
            return BadRequest(Result<ParkingReservationDto>.Failure("Invalid start time format.", ErrorCode.BadRequest));
        }

        if (!TimeSpan.TryParse(request.EndTime, out var endTime))
        {
            return BadRequest(Result<ParkingReservationDto>.Failure("Invalid end time format.", ErrorCode.BadRequest));
        }

        var command = new CreateReservationCommand(
            userId,
            request.ReservationDate,
            startTime,
            endTime,
            request.Reason);

        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<ActionResult<Result<IEnumerable<ParkingReservationDto>>>> GetMyReservations()
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<IEnumerable<ParkingReservationDto>>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new GetMyReservationsQuery(userId));
        return this.ToActionResult(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<Result<ParkingReservationDto>>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetReservationByIdQuery(id));
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    public async Task<ActionResult<Result<bool>>> Cancel(Guid id)
    {
        var userId = _userContext.GetUserId();
        if (userId == Guid.Empty)
            return Unauthorized(Result<bool>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new CancelReservationCommand(id, userId));
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<bool>>> Approve(Guid id, [FromBody] ReviewReservationRequest? request)
    {
        var adminId = _userContext.GetUserId();
        var result = await _mediator.Send(new ApproveReservationCommand(id, adminId, request?.Notes));
        return this.ToActionResult(result);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<bool>>> Reject(Guid id, [FromBody] ReviewReservationRequest? request)
    {
        var adminId = _userContext.GetUserId();
        var result = await _mediator.Send(new RejectReservationCommand(id, adminId, request?.Notes));
        return this.ToActionResult(result);
    }

    [HttpGet("admin/all")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    public async Task<ActionResult<Result<IEnumerable<ParkingReservationDto>>>> GetAll([FromQuery] ReservationStatus? status)
    {
        var result = await _mediator.Send(new GetAllReservationsQuery(status));
        return this.ToActionResult(result);
    }
}
