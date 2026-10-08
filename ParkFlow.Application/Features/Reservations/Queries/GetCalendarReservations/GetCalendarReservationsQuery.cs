using MediatR;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Queries.GetCalendarReservations;

public record CalendarReservationItem(Guid Id, string UserFullName, string? PlateNumber,
    DateTime ReservationDate, TimeSpan StartTime, TimeSpan EndTime, ReservationStatus Status, ReservationType Type);
public record CalendarReservationPage(IReadOnlyList<CalendarReservationItem> Items, int TotalCount,
    IReadOnlyDictionary<string, int> DateCounts, ReservationAvailability? Capacity = null);
public record GetCalendarReservationsQuery(DateTime Date, DateTime Month, int Page = 1,
    string? Search = null, bool GateOnly = false)
    : IRequest<Result<CalendarReservationPage>>;

public class GetCalendarReservationsHandler(IParkingReservationRepository repository)
    : IRequestHandler<GetCalendarReservationsQuery, Result<CalendarReservationPage>>
{
    public async Task<Result<CalendarReservationPage>> Handle(GetCalendarReservationsQuery request, CancellationToken cancellationToken)
    {
        var result = await repository.GetCalendarPageAsync(request.Date, request.Month, Math.Max(1, request.Page), cancellationToken,
            request.Search, request.GateOnly);
        if (request.GateOnly)
        {
            var peak = await repository.GetReservedPeakAsync(request.Date, TimeSpan.Zero, TimeSpan.FromDays(1), cancellationToken);
            result = result with { Capacity = ReservationCapacityPolicy.Describe(SystemSettingsStore.Current, peak) };
        }
        return Result<CalendarReservationPage>.Success(result, "Calendar reservations retrieved.");
    }
}
