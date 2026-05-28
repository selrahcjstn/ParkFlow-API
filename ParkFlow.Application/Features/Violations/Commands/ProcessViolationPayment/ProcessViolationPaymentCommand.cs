using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;

namespace ParkFlow.Application.Features.Violations.Commands.ProcessViolationPayment;

public record ProcessViolationPaymentCommand(
    string ReferenceNumber,
    Guid GuardUserId
) : IRequest<Result<ViolationPaymentReceiptDto>>;
