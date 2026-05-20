using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;


namespace ParkFlow.Persistence.Repositories;

public class ParkingLogHistoryRepository : IParkingLogHistoryRepository
{
    private readonly AppDbContext _context;

    public ParkingLogHistoryRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ParkingLogHistory history)
    {
        await _context.Set<ParkingLogHistory>().AddAsync(history);
        await _context.SaveChangesAsync();
    }

    public async Task<List<ParkingLogHistory>> GetByVehicleIdAsync(Guid vehicleId)
    {
        return await _context.ParkingLogHistories
            .AsNoTracking()
            .Where(h => h.VehicleId == vehicleId)
            .OrderByDescending(h => h.EntryTime)
            .ToListAsync();
    }
}