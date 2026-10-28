using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Visitors.Commands.ExitVisitorSession;

public class ExitVisitorSessionValidator : AbstractValidator<ExitVisitorSessionCommand>
{
    public ExitVisitorSessionValidator()
    {
        RuleFor(x => x.PlateNumber)
            .NotEmpty().WithMessage("Plate number is required.");
    }
}

public class ExitVisitorSessionHandler : IRequestHandler<ExitVisitorSessionCommand, Result<VisitorExitResponse>>
{
    private readonly IVisitorRepository _visitorRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IAdminRepository _adminRepository;
    private readonly ISignalRNotificationSender? _signalRNotificationSender;

    public ExitVisitorSessionHandler(
        IVisitorRepository visitorRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IAdminRepository adminRepository,
        ISignalRNotificationSender? signalRNotificationSender = null)
    {
        _visitorRepository = visitorRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _adminRepository = adminRepository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task<Result<VisitorExitResponse>> Handle(ExitVisitorSessionCommand request, CancellationToken cancellationToken)
    {
        var normalizedPlate = VisitorVehicle.NormalizePlate(request.PlateNumber);
        if (string.IsNullOrWhiteSpace(normalizedPlate))
            return Result<VisitorExitResponse>.Failure("Plate number is required.", ErrorCode.BadRequest);
        var vehicle = await _visitorRepository.GetVehicleByPlateNumberAsync(normalizedPlate);
        if (vehicle == null) return Result<VisitorExitResponse>.Failure("Visitor vehicle not found.", ErrorCode.NotFound);
        if (request.VisitorVehicleId.HasValue && request.VisitorVehicleId != vehicle.Id)
            return Result<VisitorExitResponse>.Failure("The selected vehicle does not match this plate.", ErrorCode.Conflict);
        var visitor = vehicle.Visitor;
        var activeSession = await _visitorRepository.GetActiveVisitSessionByVehicleIdAsync(vehicle.Id);
        if (activeSession == null)
            return Result<VisitorExitResponse>.Failure("No active visit session found for this vehicle.", ErrorCode.NotFound);
        if (request.SessionId.HasValue && request.SessionId != activeSession.Id)
            return Result<VisitorExitResponse>.Failure("This visit has changed. Search the vehicle again before exit.", ErrorCode.Conflict);

        // Resolve Guard / Admin
        Guid? guardProfileId = null;
        string exitGate = request.ExitGate ?? "Gate 1";

        if (request.GuardUserId.HasValue && request.GuardUserId.Value != Guid.Empty)
        {
            var userProfile = await _userProfileRepository.GetByUserIdAsync(request.GuardUserId.Value);
            if (userProfile != null)
            {
                var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
                if (guard != null)
                {
                    guardProfileId = guard.UserProfileId;
                    if (guard.AssignedGate > 0)
                    {
                        exitGate = $"Gate {guard.AssignedGate}";
                    }
                }
                else
                {
                    var admin = await _adminRepository.GetByUserProfileIdAsync(userProfile.Id);
                    if (admin != null)
                    {
                        // VisitSession.GuardId references Guards, not Admins.
                        guardProfileId = null;
                    }
                }
            }
        }

        if (!request.GuardUserId.HasValue)
            return Result<VisitorExitResponse>.Failure("An authorized guard or admin is required.", ErrorCode.Forbidden);
        var actor = await _userProfileRepository.GetByUserIdAsync(request.GuardUserId.Value);
        if (actor == null || (guardProfileId == null && await _adminRepository.GetByUserProfileIdAsync(actor.Id) == null))
            return Result<VisitorExitResponse>.Failure("An authorized guard or admin is required.", ErrorCode.Forbidden);
        activeSession.MarkExit(guardProfileId, exitGate);
        if (!await _visitorRepository.CompleteVisitSessionAsync(activeSession))
        {
            return Result<VisitorExitResponse>.Failure(
                "This visitor's exit has already been recorded.",
                ErrorCode.Conflict);
        }

        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("VisitorExitRecorded", new
                {
                    visitorId = visitor.Id,
                    sessionId = activeSession.Id,
                    plateNumber = vehicle.PlateNumber,
                    visitorVehicleId = vehicle.Id,
                    exitTime = activeSession.ExitTime,
                });
                await _signalRNotificationSender.SendToAllAsync("ParkingSessionUpdated", new { });
                await _signalRNotificationSender.SendToAllAsync("ApprovalUpdated", new { });
            }
            catch
            {
                // Silently ignore realtime dispatch failure
            }
        }

        var response = new VisitorExitResponse(
            VisitorId: visitor.Id,
            SessionId: activeSession.Id,
            FullName: visitor.FullName,
            PlateNumber: vehicle.PlateNumber,
            Brand: vehicle.Brand,
            VehicleType: vehicle.VehicleType.ToString(),
            EntryTime: activeSession.EntryTime,
            ExitTime: activeSession.ExitTime ?? DateTime.UtcNow,
            Purpose: activeSession.Purpose,
            Destination: activeSession.Destination,
            Status: activeSession.Status.ToString(),
            VisitorVehicleId: vehicle.Id
        );

        return Result<VisitorExitResponse>.Success(response, "Visitor exit recorded.");
    }
}
