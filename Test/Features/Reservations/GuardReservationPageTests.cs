using ParkFlow.Application.Features.Reservations.Queries.GetCalendarReservations;
using Xunit;

namespace Test.Features.Reservations;

public class GuardReservationPageTests
{
    private sealed class PageRepository : FakeParkingReservationRepository,
        ParkFlow.Application.Interfaces.IParkingReservationRepository
    {
        public (DateTime Date, int Page, string? Search, bool GateOnly) Captured;
        public new Task<CalendarReservationPage> GetCalendarPageAsync(DateTime date, DateTime month, int page,
            CancellationToken cancellationToken = default, string? search = null, bool gateOnly = false)
        {
            Captured = (date, page, search, gateOnly);
            return Task.FromResult(new CalendarReservationPage([], 12, new Dictionary<string, int>()));
        }
    }

    [Fact]
    public async Task GuardQueryPreservesDateSearchAndPageAndUsesGateOnlyList()
    {
        var repository = new PageRepository();
        var handler = new GetCalendarReservationsHandler(repository);
        var date = new DateTime(2026, 10, 7);
        var result = await handler.Handle(new GetCalendarReservationsQuery(date, date, 2, "ABC", true), default);
        Assert.True(result.IsSuccess);
        Assert.Equal((date, 2, "ABC", true), repository.Captured);
        Assert.Equal(12, result.Data!.TotalCount);
    }

    [Fact]
    public async Task ExistingAdminCalendarKeepsItsDefaults()
    {
        var repository = new PageRepository();
        var date = new DateTime(2026, 10, 7);
        await new GetCalendarReservationsHandler(repository).Handle(new GetCalendarReservationsQuery(date, date), default);
        Assert.Equal((date, 1, (string?)null, false), repository.Captured);
    }
}
