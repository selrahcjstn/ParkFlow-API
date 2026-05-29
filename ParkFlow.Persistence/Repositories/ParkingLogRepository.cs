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
            .FirstOrDefaultAsync(p => p.VehicleId == vehicleId && p.Status == ParkingStatus.Parked && p.ExitTime == null);
    }

    public async Task<IReadOnlyList<ParkingLog>> GetActiveParkingLogsAsync(int limit)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .Where(p => p.Status == ParkingStatus.Parked && p.ExitTime == null)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingLog>> GetTodaysParkingLogsAsync(int limit)
    {
        var today = DateTime.UtcNow.Date;

        var query = _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .Where(p => p.EntryTime.Date == today && p.Status == ParkingStatus.Parked && p.ExitTime == null)
            .OrderByDescending(p => p.CreatedAt);

        return await query.Take(limit).ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingLog>> GetRecentParkingLogsAsync(int limit)
    {
        return await _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<ParkingLog>> GetParkingHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15)
    {
        var query = _context.Set<ParkingLog>()
            .AsNoTracking()
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Student)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Personnel)
            .Include(p => p.Vehicle)
                .ThenInclude(v => v.Owner)
                    .ThenInclude(o => o.UserProfile!)
                        .ThenInclude(up => up!.Guard)
            .Include(p => p.Guard)
                .ThenInclude(g => g.UserProfile)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(p => p.Vehicle.OwnerId == userId.Value);
        }

        return await query
            .OrderByDescending(p => p.EntryTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task UpdateParkingLogAsync(ParkingLog parkingLog)
    {
        _context.Set<ParkingLog>().Update(parkingLog);
        await _context.SaveChangesAsync();
    }
}
