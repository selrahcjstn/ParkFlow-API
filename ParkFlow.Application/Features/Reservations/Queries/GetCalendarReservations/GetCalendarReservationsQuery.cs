using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Queries.GetCalendarReservations;

public record CalendarReservationItem(Guid Id, string UserFullName, string? PlateNumber,
    DateTime ReservationDate, TimeSpan StartTime, TimeSpan EndTime, ReservationStatus Status, ReservationType Type);
public record CalendarReservationPage(IReadOnlyList<CalendarReservationItem> Items, int TotalCount,
    IReadOnlyDictionary<string, int> DateCounts);
public record GetCalendarReservationsQuery(DateTime Date, DateTime Month, int Page = 1)
    : IRequest<Result<CalendarReservationPage>>;

public class GetCalendarReservationsHandler(IParkingReservationRepository repository)
    : IRequestHandler<GetCalendarReservationsQuery, Result<CalendarReservationPage>>
{
    public async Task<Result<CalendarReservationPage>> Handle(GetCalendarReservationsQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.GetCalendarPageAsync(request.Date, request.Month, Math.Max(1, request.Page), cancellationToken);
        return Result<CalendarReservationPage>.Success(result, "Calendar reservations retrieved.");
    }
}
