using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;

namespace ParkFlow.Application.Features.Violations.Queries.CheckViolationPayment;

public record CheckViolationPaymentQuery(string ReferenceNumber)
    : IRequest<Result<ViolationPaymentStatusDto>>;
