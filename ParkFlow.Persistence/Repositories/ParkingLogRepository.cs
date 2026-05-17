using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class ParkingLogRepository : IParkingLogRepository
{
    private readonly AppDbContext _context;
    public ParkingLogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddParkingLogAsync(ParkingLog parkingLog)
    {
        await _context.Set<ParkingLog>().AddAsync(parkingLog);
        await _context.SaveChangesAsync();
    }

    public async Task<ParkingLog?> GetActiveParkingLogByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.VehicleId == vehicleId && p.Status == ParkingStatus.Parked);
    }

    public async Task<(IReadOnlyList<ParkingLog> Items, int TotalCount)> GetTodaysParkingLogsAsync(int limit)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
            .Where(p => p.EntryTime.Date == today)
            .OrderByDescending(p => p.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query.Take(limit).ToListAsync();

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task UpdateParkingLogAsync(ParkingLog parkingLog)
    {
        _context.Set<ParkingLog>().Update(parkingLog);
        await _context.SaveChangesAsync();
    }
}
