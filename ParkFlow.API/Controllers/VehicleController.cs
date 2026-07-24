using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId;
using ParkFlow.Application.Interfaces;

using ParkFlow.Domain.Enums;

namespace ParkFlow.API.Controllers;

public record UpdateVehicleRequest(
    string PlateNumber,
    string Brand,
    VehicleType VehicleType
);

[Route("api/vehicles")]
[ApiController]
public class VehicleController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUserContext _userContext;

    public VehicleController(IMediator mediator, IUserContext userContext)
    {
        _mediator = mediator;
        _userContext = userContext;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<Result<IEnumerable<ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles.AdminVehicleDto>>>> GetAll()
    {
        var result = await _mediator.Send(new ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles.GetAllVehiclesQuery());
        return this.ToActionResult(result);
    }

    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create(CreateVehicleCommand command)
    {
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpGet("owner/my")]
    public async Task<ActionResult<Result<IEnumerable<ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId.VehicleDto>>>> GetMine()
    {
        var ownerId = _userContext.GetUserId();
        if (ownerId == Guid.Empty)
            return Unauthorized(Result<IEnumerable<ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId.VehicleDto>>.Failure("User not identified.", ErrorCode.Unauthorized));

        var result = await _mediator.Send(new GetVehiclesByOwnerIdQuery(ownerId));
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> Update(Guid id, [FromBody] UpdateVehicleRequest request)
    {
        var ownerId = _userContext.GetUserId();
        if (ownerId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new UpdateVehicleCommand(id, ownerId, request.PlateNumber, request.Brand, request.VehicleType);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> Delete(Guid id)
    {
        var ownerId = _userContext.GetUserId();
        if (ownerId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new DeleteVehicleCommand(id, ownerId);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("{id:guid}/set-primary")]
    public async Task<ActionResult<Result<Guid>>> SetPrimary(Guid id)
    {
        var ownerId = _userContext.GetUserId();
        if (ownerId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var command = new SetPrimaryVehicleCommand(id, ownerId);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }
}
