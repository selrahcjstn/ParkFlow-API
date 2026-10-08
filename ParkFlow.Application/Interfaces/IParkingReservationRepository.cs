using ParkFlow.Domain.Entities;
using ParkFlow.Application.Features.Reservations.Queries.GetCalendarReservations;
using ParkFlow.Domain.Enums;
using ParkFlow.Application.Features.Reservations;

namespace ParkFlow.Application.Interfaces;

public interface IParkingReservationRepository
{
    Task<int> GetReservedPeakAsync(DateTime date, TimeSpan start, TimeSpan end, CancellationToken cancellationToken = default);
    Task<ReservationBookingResult> TryAddWithinCapacityAsync(ParkingReservation reservation, int capacity, CancellationToken cancellationToken = default);
    Task<CalendarReservationPage> GetCalendarPageAsync(DateTime date, DateTime month, int page,
        CancellationToken cancellationToken = default, string? search = null, bool gateOnly = false);
    async Task<IEnumerable<ParkingReservation>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var reservations = new List<ParkingReservation>();
        foreach (var id in userIds.Distinct())
            reservations.AddRange(await GetByUserIdAsync(id));
        return reservations;
    }
    Task AddAsync(ParkingReservation reservation);
    Task<ParkingReservation?> GetByIdAsync(Guid id);
    Task<IEnumerable<ParkingReservation>> GetByUserIdAsync(Guid userId);
    Task<ParkingReservation?> GetByReferenceNumberAsync(string referenceNumber);
    Task<IEnumerable<ParkingReservation>> GetAllAsync(ReservationStatus? status = null);
    Task UpdateAsync(ParkingReservation reservation);
    Task SaveChangesAsync();
}
