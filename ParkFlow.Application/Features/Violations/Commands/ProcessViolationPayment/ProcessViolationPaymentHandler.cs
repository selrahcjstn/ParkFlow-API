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

    public ProcessViolationPaymentHandler(
        IViolationRepository violationRepository,
        IUserProfileRepository userProfileRepository,
        IGuardRepository guardRepository,
        IParkingLogRepository parkingLogRepository,
        IValidator<ProcessViolationPaymentCommand> validator)
    {
        _violationRepository = violationRepository;
        _userProfileRepository = userProfileRepository;
        _guardRepository = guardRepository;
        _parkingLogRepository = parkingLogRepository;
        _validator = validator;
    }

    public async Task<Result<ViolationPaymentReceiptDto>> Handle(ProcessViolationPaymentCommand request, CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            var errors = string.Join(", ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ViolationPaymentReceiptDto>.Failure(errors, ErrorCode.BadRequest);
        }

        // 1. Verify that the user processing this payment is a Guard
        var userProfile = await _userProfileRepository.GetByUserIdAsync(request.GuardUserId);
        if (userProfile == null)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                "User profile not found for the current user.",
                ErrorCode.NotFound);
        }

        var guard = await _guardRepository.GetByUserProfileIdAsync(userProfile.Id);
        if (guard == null)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                "Access Denied: Only guards can verify and process violation payments.",
                ErrorCode.Forbidden);
        }

        // 2. Fetch the violation by ReferenceNumber
        var violation = await _violationRepository.GetByReferenceNumberAsync(request.ReferenceNumber);
        if (violation == null)
        {
            return Result<ViolationPaymentReceiptDto>.Failure(
                $"No violation found with reference number '{request.ReferenceNumber}'.",
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

        return Result<ViolationPaymentReceiptDto>.Success(
            receipt,
            $"Violation reference '{request.ReferenceNumber}' has been successfully processed and marked as settled.");
    }
}
