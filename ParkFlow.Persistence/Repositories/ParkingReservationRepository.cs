using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Features.Reservations.Queries.GetCalendarReservations;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

public class ParkingReservationRepository : IParkingReservationRepository
{
    private readonly AppDbContext _context;

    public ParkingReservationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CalendarReservationPage> GetCalendarPageAsync(DateTime date, DateTime month, int page,
        CancellationToken cancellationToken = default, string? search = null, bool gateOnly = false)
    {
        var start = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var monthStart = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var query = _context.ParkingReservations.AsNoTracking();
        var daily = query.Where(r => r.ReservationDate >= start && r.ReservationDate < end);
        if (gateOnly)
            daily = daily.Where(r => r.Status == ReservationStatus.Approved || r.Status == ReservationStatus.Completed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            daily = daily.Where(r => r.ReferenceNumber.ToLower().Contains(term)
                || (r.Vehicle != null && r.Vehicle.PlateNumber.ToLower().Contains(term))
                || (r.UserAccount.UserProfile != null &&
                    (r.UserAccount.UserProfile.FirstName + " " + r.UserAccount.UserProfile.LastName).ToLower().Contains(term)));
        }
        var total = await daily.CountAsync(cancellationToken);
        var items = await daily.OrderBy(r => r.StartTime).ThenBy(r => r.Id)
            .Skip((Math.Max(1, page) - 1) * 5).Take(5)
            .Select(r => new CalendarReservationItem(r.Id,
                r.UserAccount.UserProfile == null ? "" :
                    r.UserAccount.UserProfile.FirstName + " " + r.UserAccount.UserProfile.LastName,
                r.Vehicle == null ? null : r.Vehicle.PlateNumber,
                r.ReservationDate, r.StartTime, r.EndTime, r.Status, r.Type))
            .ToListAsync(cancellationToken);
        // Guards only need today's list, not an entire month of calendar counts.
        if (gateOnly)
            return new CalendarReservationPage(items, total, new Dictionary<string, int>());
        var counts = await query.Where(r => r.ReservationDate >= monthStart && r.ReservationDate < monthEnd)
            .GroupBy(r => r.ReservationDate.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return new CalendarReservationPage(items, total,
            counts.ToDictionary(row => row.Date.ToString("yyyy-MM-dd"), row => row.Count));
    }

    public async Task<IEnumerable<ParkingReservation>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await _context.ParkingReservations.AsNoTracking()
            .Where(reservation => ids.Contains(reservation.UserId))
            .OrderByDescending(reservation => reservation.CreatedAt).ToListAsync();
    }

    public async Task AddAsync(ParkingReservation reservation)
    {
        await _context.ParkingReservations.AddAsync(reservation);
    }

    public async Task<ParkingReservation?> GetByIdAsync(Guid id)
    {
        return await _context.ParkingReservations
            .Include(r => r.Vehicle)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<IEnumerable<ParkingReservation>> GetByUserIdAsync(Guid userId)
    {
        return await _context.ParkingReservations
            .Include(r => r.Vehicle)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ParkingReservation?> GetByReferenceNumberAsync(string referenceNumber)
    {
        return await _context.ParkingReservations
            .Include(r => r.Vehicle)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .FirstOrDefaultAsync(r => r.ReferenceNumber == referenceNumber);
    }

    public async Task<IEnumerable<ParkingReservation>> GetAllAsync(ReservationStatus? status = null)
    {
        var query = _context.ParkingReservations
            .Include(r => r.Vehicle)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.UserProfile)
            .Include(r => r.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public Task UpdateAsync(ParkingReservation reservation)
    {
        _context.ParkingReservations.Update(reservation);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
