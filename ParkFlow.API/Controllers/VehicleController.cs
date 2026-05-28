using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Vehicles.Command;
using ParkFlow.Application.Features.Vehicles.Queries.GetVehiclesByOwnerId;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.API.Controllers;

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
}
