using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Application.Features.Violations.Queries.CheckViolationPayment;

public class CheckViolationPaymentHandler
    : IRequestHandler<CheckViolationPaymentQuery, Result<ViolationPaymentStatusDto>>
{
    private readonly IViolationRepository _violationRepository;

    public CheckViolationPaymentHandler(IViolationRepository violationRepository)
    {
        _violationRepository = violationRepository;
    }

    public async Task<Result<ViolationPaymentStatusDto>> Handle(
        CheckViolationPaymentQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
            return Result<ViolationPaymentStatusDto>.Failure(
                "Reference number is required.",
                ErrorCode.BadRequest);

        var violation = await _violationRepository.GetByReferenceNumberAsync(request.ReferenceNumber);

        if (violation is null)
            return Result<ViolationPaymentStatusDto>.Failure(
                $"No violation found with reference number '{request.ReferenceNumber}'.",
                ErrorCode.NotFound);

        var isPaid = violation.SettlementStatus == global::SettlementStatus.Settled;

        var dto = new ViolationPaymentStatusDto
        {
            ReferenceNumber   = violation.ReferenceNumber,
            ViolationType     = violation.ViolationType.ToString(),
            PenaltyFee        = violation.PenaltyFee,
            SettlementStatus  = violation.SettlementStatus.ToString(),
            IsPaid            = isPaid,
            IssuedAt          = violation.CreatedAt
        };

        var message = isPaid
            ? "Violation has already been paid."
            : "Violation is still unpaid.";

        return Result<ViolationPaymentStatusDto>.Success(dto, message);
    }
}
