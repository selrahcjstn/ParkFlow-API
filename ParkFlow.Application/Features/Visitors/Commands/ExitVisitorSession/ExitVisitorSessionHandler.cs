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
        var normalizedPlate = request.PlateNumber.Trim().ToUpper();

        var visitor = await _visitorRepository.GetByPlateNumberAsync(normalizedPlate);
        if (visitor == null)
        {
            return Result<VisitorExitResponse>.Failure("Visitor record not found.", ErrorCode.NotFound);
        }

        var activeSession = await _visitorRepository.GetActiveVisitSessionByVisitorIdAsync(visitor.Id);
        if (activeSession == null)
        {
            return Result<VisitorExitResponse>.Failure("No active visit session found for this visitor (visitor is not inside campus).", ErrorCode.NotFound);
        }

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
                        guardProfileId = admin.UserProfileId;
                    }
                }
            }
        }

        activeSession.MarkExit(guardProfileId, exitGate);
        await _visitorRepository.UpdateVisitSessionAsync(activeSession);

        if (_signalRNotificationSender != null)
        {
            try
            {
                await _signalRNotificationSender.SendToAllAsync("VisitorExitRecorded", new
                {
                    visitorId = visitor.Id,
                    sessionId = activeSession.Id,
                    plateNumber = visitor.PlateNumber,
                    exitTime = activeSession.ExitTime
                });
                await _signalRNotificationSender.SendToAllAsync("ParkingSessionUpdated", new { });
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
            PlateNumber: visitor.PlateNumber,
            Brand: visitor.Brand,
            VehicleType: visitor.VehicleType.ToString(),
            EntryTime: activeSession.EntryTime,
            ExitTime: activeSession.ExitTime ?? DateTime.UtcNow,
            Purpose: activeSession.Purpose,
            Destination: activeSession.Destination,
            Status: activeSession.Status.ToString()
        );

        return Result<VisitorExitResponse>.Success(response, "Visitor exit recorded successfully.");
    }
}
