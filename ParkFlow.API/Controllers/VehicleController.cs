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

    [Authorize]
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

    [Authorize]
    [HttpPatch("{id:guid}/validate")]
    public async Task<ActionResult<Result<Guid>>> ValidateVehicle(
        Guid id,
        [FromBody] ValidateVehicleRequest request,
        [FromServices] IVehicleRepository vehicleRepository,
        [FromServices] IUserAccountRepository userAccountRepository,
        [FromServices] ICorSubmissionRepository corSubmissionRepository,
        [FromServices] INotificationService? notificationService = null,
        [FromServices] IEmailService? emailService = null,
        [FromServices] ISignalRNotificationSender? signalRNotificationSender = null)
    {
        var vehicle = await vehicleRepository.GetByIdAsync(id);
        if (vehicle == null)
            return NotFound(Result<Guid>.Failure("Vehicle not found.", ErrorCode.NotFound));

        vehicle.UpdateVerificationStatus(request.VerificationStatus, request.RejectionReason);
        await vehicleRepository.UpdateAsync(vehicle);

        var user = await userAccountRepository.GetByIdAsync(vehicle.OwnerId);
        if (user != null)
        {
            if (request.VerificationStatus == CorVerificationStatus.Verified)
            {
                var submission = await corSubmissionRepository.GetLatestByUserIdAsync(vehicle.OwnerId);
                if (submission == null || submission.VerificationStatus == CorVerificationStatus.Verified)
                {
                    user.Verify();
                    await userAccountRepository.UpdateAsync(user);
                }
            }

            var isApproved = request.VerificationStatus == CorVerificationStatus.Verified;

            if (signalRNotificationSender != null)
            {
                var eventData = new
                {
                    userId = user.Id,
                    vehicleId = vehicle.Id,
                    type = isApproved ? "vehicle_approved" : "vehicle_rejected",
                    title = isApproved ? "Vehicle Approved" : "Vehicle Registration Rejected",
                    body = isApproved
                        ? "Your vehicle information has been verified and approved."
                        : (request.RejectionReason ?? "Your vehicle registration was rejected.")
                };
                try
                {
                    await signalRNotificationSender.SendToUserAsync(user.Id.ToString(), "VerificationStatusChanged", eventData);
                    await signalRNotificationSender.SendToAllAsync("VerificationStatusChanged", eventData);
                    await signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", eventData);
                }
                catch
                {
                    // Ignore transient SignalR send failures
                }
            }

            if (notificationService != null)
            {
                var title = isApproved ? "Vehicle Approved" : "Vehicle Registration Rejected";
                var subtitle = isApproved ? "Vehicle Verified" : "Action Required";
                var body = isApproved
                    ? "Your vehicle information has been verified and approved."
                    : string.IsNullOrWhiteSpace(request.RejectionReason)
                        ? "Your vehicle registration was rejected. Please review the reason and update/re-upload your vehicle documents."
                        : $"Your vehicle registration was rejected. Reason: {request.RejectionReason}. Please update/re-upload your vehicle documents.";
                var actionRoute = "/(settings)/vehicle";
                var actionText = isApproved ? "View Vehicle" : "Fix Vehicle Info";
                var type = isApproved ? "vehicle_approved" : "vehicle_rejected";

                await notificationService.CreateAndSendNotificationAsync(
                    user.Id,
                    title,
                    body,
                    type: type,
                    subtitle: subtitle,
                    actionRoute: actionRoute,
                    actionText: actionText,
                    priority: "high",
                    issuer: "ParkFlow Vehicle Desk"
                );
            }

            if (emailService != null && !string.IsNullOrWhiteSpace(user.PrimaryEmail))
            {
                try
                {
                    var emailSubject = isApproved
                        ? "ParkFlow - Vehicle Approved"
                        : "ParkFlow - Vehicle Registration Rejected";

                    var reasonBlock = !isApproved && !string.IsNullOrWhiteSpace(request.RejectionReason)
                        ? $"<div style='background-color:#FEF2F2; border-left:4px solid #EF4444; padding:12px; margin:16px 0; font-family:sans-serif;'><strong>Rejection Reason:</strong> {request.RejectionReason}</div>"
                        : "";

                    var emailBody = isApproved
                        ? $@"
                            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #E2E8F0; border-radius:12px;'>
                              <h2 style='color:#10B981; margin-top:0;'>Vehicle Approved</h2>
                              <p>Hello,</p>
                              <p>Your vehicle registration has been approved by ParkFlow Security Administration.</p>
                              <p style='color:#64748B; font-size:12px; margin-top:24px;'>ParkFlow Security Administration</p>
                            </div>"
                        : $@"
                            <div style='font-family:sans-serif; max-width:600px; margin:0 auto; padding:20px; border:1px solid #E2E8F0; border-radius:12px;'>
                              <h2 style='color:#EF4444; margin-top:0;'>Vehicle Registration Rejected</h2>
                              <p>Hello,</p>
                              <p>Your vehicle registration was rejected. Please review the reason and update/re-upload your required vehicle documents.</p>
                              {reasonBlock}
                              <p>Please log in to the ParkFlow mobile app to update your vehicle information.</p>
                              <p style='color:#64748B; font-size:12px; margin-top:24px;'>ParkFlow Security Administration</p>
                            </div>";

                    await emailService.SendEmailAsync(user.PrimaryEmail, emailSubject, emailBody);
                }
                catch
                {
                    // Ignore email dispatch failure
                }
            }
        }

        if (signalRNotificationSender != null)
        {
            try
            {
                await signalRNotificationSender.SendToAllAsync("ApprovalListUpdated", new
                {
                    type = "vehicle",
                    vehicleId = vehicle.Id,
                    ownerId = vehicle.OwnerId,
                    status = request.VerificationStatus.ToString()
                });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        return Ok(Result<Guid>.Success(vehicle.Id, $"Vehicle verification status updated to {request.VerificationStatus}."));
    }

    [Authorize]
    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<Result<Guid>>> ValidateVehiclePost(
        Guid id,
        [FromBody] ValidateVehicleRequest request,
        [FromServices] IVehicleRepository vehicleRepository,
        [FromServices] IUserAccountRepository userAccountRepository,
        [FromServices] ICorSubmissionRepository corSubmissionRepository,
        [FromServices] INotificationService? notificationService = null,
        [FromServices] IEmailService? emailService = null,
        [FromServices] ISignalRNotificationSender? signalRNotificationSender = null)
    {
        return await ValidateVehicle(id, request, vehicleRepository, userAccountRepository, corSubmissionRepository, notificationService, emailService, signalRNotificationSender);
    }

    [Authorize]
    [HttpGet("lookup/{plateNumber}")]
    public async Task<ActionResult<Result<ParkFlow.Application.Features.Visitors.DTOs.UnifiedPlateLookupDto>>> LookupByPlate(string plateNumber)
    {
        var result = await _mediator.Send(new ParkFlow.Application.Features.Visitors.Queries.GetPlateLookup.GetPlateLookupQuery(plateNumber));
        return this.ToActionResult(result);
    }
}

