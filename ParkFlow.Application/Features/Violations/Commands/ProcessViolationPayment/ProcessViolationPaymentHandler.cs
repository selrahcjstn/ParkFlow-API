using FluentValidation;
using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Violations.Commands.ProcessViolationPayment;

public class ProcessViolationPaymentHandler : IRequestHandler<ProcessViolationPaymentCommand, Result<ViolationPaymentReceiptDto>>
{
    private readonly IViolationRepository _violationRepository;
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly IGuardRepository _guardRepository;
    private readonly IParkingLogRepository _parkingLogRepository;
    private readonly IValidator<ProcessViolationPaymentCommand> _validator;
    private readonly ISignalRNotificationSender _notificationSender;
    private readonly INotificationService? _notificationService;

    private readonly IAdminRepository? _adminRepository;

    public ProcessViolationPaymentHandler(
        IViolationRepository violationRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IParkingLogRepository parkingLogRepository,
        IValidator<ProcessViolationPaymentCommand> validator,
        ISignalRNotificationSender notificationSender,
        INotificationService? notificationService = null,
        IAdminRepository? adminRepository = null)
    {
        _violationRepository = violationRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _parkingLogRepository = parkingLogRepository;
        _validator = validator;
        _notificationSender = notificationSender;
        _notificationService = notificationService;
        _adminRepository = adminRepository;
    }

    public async Task<Result<ViolationPaymentReceiptDto>> Handle(ProcessViolationPaymentCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ViolationPaymentReceiptDto>.Failure(errors, ErrorCode.BadRequest);
        }

        // 1. Verify that the user processing this payment is a Guard or Admin
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.GuardUserId);
        if (userProfile == null)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                "User profile not found for the current user.",
                ErrorCode.NotFound);
        }

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
        var admin = _adminRepository != null ? await _adminRepository.GetByUserProfileIdAsync(userProfile.Id) : null;
        if (guard == null && admin == null)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                "Access Denied: Only guards or administrators can verify and process violation payments.",
                ErrorCode.Forbidden);
        }

        // 2. Fetch the violation by ReferenceNumber (or fallback to log id / plate number)
        var trimmedRef = request.ReferenceNumber.Trim();
        var violation = await _violationRepository.GetByReferenceNumberAsync(trimmedRef);

        if (violation == null && Guid.TryParse(trimmedRef, out var parsedGuid))
        {
            violation = await _violationRepository.GetByIdAsync(parsedGuid)
                     ?? await _violationRepository.GetByLogIdAsync(parsedGuid);
        }

        if (violation == null && trimmedRef.StartsWith("VIO-LOG-", StringComparison.OrdinalIgnoreCase))
        {
            var logPrefix = trimmedRef.Replace("VIO-LOG-", "", StringComparison.OrdinalIgnoreCase).Trim();
            var recentLogs = await _parkingLogRepository.GetRecentParkingLogsAsync(100);
            var matchingLog = recentLogs.FirstOrDefault(l => l.Id.ToString().ToUpper().StartsWith(logPrefix.ToUpper()));
            if (matchingLog != null)
            {
                violation = await _violationRepository.GetByLogIdAsync(matchingLog.Id);
                if (violation == null)
                {
                    violation = new Violation(matchingLog.Id, 100.00m);
                    await _violationRepository.AddAsync(violation);
                }
            }
        }

        if (violation == null)
        {
            violation = await _violationRepository.GetLatestUnsettledByPlateNumberAsync(trimmedRef);
        }

        if (violation == null)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                $"No violation found matching reference or vehicle '{request.ReferenceNumber}'.",
                ErrorCode.NotFound);
        }

        // 3. Check if already settled
        if (violation.SettlementStatus == SettlementStatus.Settled)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                "Violation is already marked as paid.",
                ErrorCode.BadRequest);
        }

        // 4. Mark as paid
        violation.MarkAsPaid();
        await _violationRepository.UpdateAsync(violation);

        // 5. Construct and return receipt
        var log = violation.ParkingLog;
        var vehicle = log?.Vehicle;
        var ownerProfile = vehicle?.Owner?.UserProfile;
        var guardMiddle = string.IsNullOrWhiteSpace(userProfile.MiddleName) ? "" : $" {userProfile.MiddleName}";
        var guardName = $"{userProfile.FirstName}{guardMiddle} {userProfile.LastName}";

        var receipt = new ViolationPaymentReceiptDto
        {
            ReferenceNumber = violation.ReferenceNumber,
            ViolationType = violation.ViolationType.ToString(),
            PenaltyFee = violation.PenaltyFee,
            SettlementStatus = violation.SettlementStatus.ToString(),
            PaidAt = DateTime.UtcNow,

            // Owner Info
            OwnerFirstName = ownerProfile?.FirstName ?? "N/A",
            OwnerLastName = ownerProfile?.LastName ?? "N/A",
            OwnerMiddleName = ownerProfile?.MiddleName,

            // Vehicle Info
            PlateNumber = vehicle?.PlateNumber ?? "N/A",
            VehicleBrand = vehicle?.Brand ?? "N/A",
            VehicleType = vehicle?.VehicleType.ToString() ?? "N/A",

            // Processor Info
            GuardName = guardName
        };

        var notificationData = new
        {
            ReferenceNumber = violation.ReferenceNumber,
            PlateNumber = vehicle?.PlateNumber ?? "N/A",
            IsPaid = true,
            SettlementStatus = "Settled",
            PaidAt = receipt.PaidAt,
            Amount = violation.PenaltyFee
        };

        if (_notificationService != null && vehicle?.OwnerId != null && vehicle.OwnerId != Guid.Empty)
        {
            await _notificationService.CreateAndSendNotificationAsync(
                vehicle.OwnerId,
                "Violation Citation Paid",
                $"Payment of ₱{violation.PenaltyFee:0.00} for citation [{violation.ReferenceNumber}] was processed. Clearance verified.",
                type: "approved",
                subtitle: "Account Status Cleared",
                referenceCode: violation.ReferenceNumber,
                vehiclePlate: vehicle.PlateNumber,
                actionRoute: "/(settings)/violations",
                actionText: "View Violations History",
                issuer: "ParkFlow Financial Desk",
                signalRData: notificationData
            );
        }
        else
        {
            try
            {
                if (vehicle?.OwnerId != null && vehicle.OwnerId != Guid.Empty)
                {
                    await _notificationSender.SendToUserAsync(vehicle.OwnerId.ToString(), "PaymentProcessed", notificationData);
                }
            }
            catch { }
        }

        return Result<ViolationPaymentReceiptDto>.Success(
            receipt,
            $"Violation reference '{request.ReferenceNumber}' has been successfully processed and marked as settled.");
    }
}
