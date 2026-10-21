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
    VehicleType VehicleType,
    string? OrcrDocumentUrl = null,
    string? VehiclePictureUrl = null
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

    [Authorize(Policy = "AdminOrSuperAdmin")]
    [HttpGet]
    public async Task<ActionResult<Result<IEnumerable<ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles.AdminVehicleDto>>>> GetAll()
    {
        var result = await _mediator.Send(new ParkFlow.Application.Features.Vehicles.Queries.GetAllVehicles.GetAllVehiclesQuery());
        Response.Headers.CacheControl = "no-store";
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpPost("create")]
    public async Task<ActionResult<Result<Guid>>> Create(CreateVehicleCommand command)
    {
        if (command.OwnerId != _userContext.GetUserId()) return Forbid();
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

        var command = new UpdateVehicleCommand(id, ownerId, request.PlateNumber, request.Brand, request.VehicleType, request.OrcrDocumentUrl, request.VehiclePictureUrl);
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

        var isAdmin = User.IsInRole("Admin") ||
                      User.IsInRole("SuperAdmin") ||
                      User.HasClaim("profile_type", "admin") ||
                      User.HasClaim("profile_type", "superadmin") ||
                      User.HasClaim("role", "Admin") ||
                      User.HasClaim("role", "SuperAdmin");

        var command = new DeleteVehicleCommand(id, ownerId, isAdmin);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize(Policy = "AdminOrSuperAdmin")]
    [HttpDelete("admin/{id:guid}")]
    public async Task<ActionResult<Result<Guid>>> DeleteAdmin(Guid id)
    {
        var ownerId = _userContext.GetUserId();
        var command = new DeleteVehicleCommand(id, ownerId, IsAdmin: true);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

    [Authorize]
    [HttpDelete("plate/{plateNumber}")]
    public async Task<ActionResult<Result<Guid>>> DeleteByPlate(
        string plateNumber,
        [FromServices] IVehicleRepository vehicleRepository)
    {
        var ownerId = _userContext.GetUserId();
        if (ownerId == Guid.Empty)
            return Unauthorized(Result<Guid>.Failure("User not identified.", ErrorCode.Unauthorized));

        var vehicle = await vehicleRepository.GetByPlateNumberAsync(plateNumber);
        if (vehicle == null)
            return NotFound(Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound));

        var isAdmin = User.IsInRole("Admin") ||
                      User.IsInRole("SuperAdmin") ||
                      User.HasClaim("profile_type", "admin") ||
                      User.HasClaim("profile_type", "superadmin") ||
                      User.HasClaim("role", "Admin") ||
                      User.HasClaim("role", "SuperAdmin");

        var command = new DeleteVehicleCommand(vehicle.Id, ownerId, isAdmin);
        var result = await _mediator.Send(command);
        return this.ToActionResult(result);
    }

public record ValidateVehicleRequest(
    CorVerificationStatus VerificationStatus,
    string? RejectionReason = null
);

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

    [Authorize(Policy = "AdminOrSuperAdmin")]
    [HttpPatch("{id:guid}/validate")]
    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<Result<Guid>>> ValidateVehicle(Guid id, [FromBody] ValidateVehicleRequest request)
    {
        if (request.VerificationStatus is not (CorVerificationStatus.Verified or CorVerificationStatus.Rejected)) return BadRequest("Choose approve or reject.");
        var status = request.VerificationStatus == CorVerificationStatus.Verified ? "Approved" : "Rejected";
        var result = await _mediator.Send(new ParkFlow.Application.Features.Verification.ReviewVerificationSubject("Vehicle", id,
            status, request.RejectionReason ?? (status == "Approved" ? "Ownership documents reviewed by an administrator." : "Vehicle submission rejected by an administrator."), _userContext.GetUserId()));
        return this.ToActionResult(result.IsSuccess ? Result<Guid>.Success(id, result.Message) : Result<Guid>.Failure(result.Message, result.ErrorCode));
    }

    [Authorize]
    [HttpGet("lookup/{plateNumber}")]
    public async Task<ActionResult<Result<ParkFlow.Application.Features.Visitors.DTOs.UnifiedPlateLookupDto>>> LookupByPlate(string plateNumber)
    {
        var result = await _mediator.Send(new ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup.GetPlateLookupQuery(plateNumber));
        return this.ToActionResult(result);
    }
}

