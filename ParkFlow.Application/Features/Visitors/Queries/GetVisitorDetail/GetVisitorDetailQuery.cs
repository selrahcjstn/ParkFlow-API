using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Visitors.Queries.GetVisitorDetail;

public record GetVisitorDetailQuery(Guid Id, int Page = 1, int PageSize = 20) : IRequest<Result<VisitorDetailDto>>;

public class GetVisitorDetailHandler : IRequestHandler<GetVisitorDetailQuery, Result<VisitorDetailDto>>
{
    private readonly IVisitorRepository _visitorRepository;

    public GetVisitorDetailHandler(IVisitorRepository visitorRepository)
    {
        _visitorRepository = visitorRepository;
    }

    public async Task<Result<VisitorDetailDto>> Handle(GetVisitorDetailQuery request, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var size = request.PageSize < 1 ? 20 : Math.Min(100, request.PageSize);
        var detail = await _visitorRepository.GetDetailPageAsync(request.Id, page, size);
        return detail == null
            ? Result<VisitorDetailDto>.Failure("Visitor not found.", ErrorCode.NotFound)
            : Result<VisitorDetailDto>.Success(detail, "Visitor details retrieved successfully.");
    }
}
