using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Features.Visitors.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Features.Visitors.Queries.GetVisitorDetail;

public record GetVisitorDetailQuery(Guid Id) : IRequest<Result<VisitorDetailDto>>;

public class GetVisitorDetailHandler : IRequestHandler<GetVisitorDetailQuery, Result<VisitorDetailDto>>
{
    private readonly IVisitorRepository _visitorRepository;

    public GetVisitorDetailHandler(IVisitorRepository visitorRepository)
    {
        _visitorRepository = visitorRepository;
    }

    public async Task<Result<VisitorDetailDto>> Handle(GetVisitorDetailQuery request, CancellationToken cancellationToken)
    {
        var visitor = await _visitorRepository.GetWithSessionsByIdAsync(request.Id);
        if (visitor == null)
        {
            return Result<VisitorDetailDto>.Failure("Visitor not found.", ErrorCode.NotFound);
        }

        var sessions = visitor.VisitSessions.OrderByDescending(s => s.EntryTime).ToList();
        var lastSession = sessions.FirstOrDefault();
        var isInside = sessions.Any(s => s.Status == VisitSessionStatus.Inside);

        var visitorDto = new VisitorDto(
            Id: visitor.Id,
            FullName: visitor.FullName,
            PlateNumber: visitor.PlateNumber,
            Brand: visitor.Brand,
            VehicleType: (int)visitor.VehicleType,
            ContactNumber: visitor.ContactNumber,
            LastVisit: lastSession?.EntryTime,
            TotalVisits: sessions.Count,
            IsInside: isInside
        );

        var visitDtos = sessions.Select(s =>
        {
            var entryGuardName = s.EntryGuard?.UserProfile != null
                ? $"{s.EntryGuard.UserProfile.FirstName} {s.EntryGuard.UserProfile.LastName}".Trim()
                : null;

            var exitGuardName = s.ExitGuard?.UserProfile != null
                ? $"{s.ExitGuard.UserProfile.FirstName} {s.ExitGuard.UserProfile.LastName}".Trim()
                : null;

            return new VisitSessionDto(
                Id: s.Id,
                EntryTime: s.EntryTime,
                ExitTime: s.ExitTime,
                Purpose: s.Purpose,
                Destination: s.Destination,
                EntryGuardName: entryGuardName,
                ExitGuardName: exitGuardName,
                EntryGate: s.EntryGate,
                ExitGate: s.ExitGate,
                Status: s.Status.ToString()
            );
        }).ToList();

        var detailDto = new VisitorDetailDto(
            Visitor: visitorDto,
            Visits: visitDtos
        );

        return Result<VisitorDetailDto>.Success(detailDto, "Visitor details retrieved successfully.");
    }
}
