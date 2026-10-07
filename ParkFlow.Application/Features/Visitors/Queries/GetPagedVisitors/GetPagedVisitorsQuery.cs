using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Visitors.Queries.GetPagedVisitors;

public record GetPagedVisitorsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool? OnlyInside = null
) : IRequest<Result<PagedVisitorsResponse>>;

public class GetPagedVisitorsHandler : IRequestHandler<GetPagedVisitorsQuery, Result<PagedVisitorsResponse>>
{
    private readonly IVisitorRepository _visitorRepository;

    public GetPagedVisitorsHandler(IVisitorRepository visitorRepository)
    {
        _visitorRepository = visitorRepository;
    }

    public async Task<Result<PagedVisitorsResponse>> Handle(GetPagedVisitorsQuery request, CancellationToken cancellationToken)
    {
        var page = request.PageNumber < 1 ? 1 : request.PageNumber;
        var size = request.PageSize < 1 ? 20 : (request.PageSize > 100 ? 100 : request.PageSize);

        var (items, totalCount) = await _visitorRepository.GetPagedVisitorsAsync(page, size, request.Search, request.OnlyInside);

        var hasMore = (page * size) < totalCount;

        var response = new PagedVisitorsResponse(
            Items: items,
            Page: page,
            PageSize: size,
            TotalCount: totalCount,
            HasMore: hasMore
        );

        return Result<PagedVisitorsResponse>.Success(response, "Visitors retrieved successfully.");
    }
}
