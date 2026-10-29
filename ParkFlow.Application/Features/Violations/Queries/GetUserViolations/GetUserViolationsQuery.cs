using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;

namespace ParkFlow.Application.Features.Violations.Queries.GetUserViolations;

public record GetUserViolationsQuery(Guid UserId)
    : IRequest<Result<IEnumerable<ViolationHistoryResponse>>>;
