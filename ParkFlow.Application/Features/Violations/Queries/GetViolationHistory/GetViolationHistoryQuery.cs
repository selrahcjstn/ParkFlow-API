using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Violations.DTOs;

namespace ParkFlow.Application.Features.Violations.Queries.GetViolationHistory;

public record GetViolationHistoryQuery(Guid UserId, int PageNumber = 1, int PageSize = 15)
    : IRequest<Result<PagedViolationHistoryResponse>>;
