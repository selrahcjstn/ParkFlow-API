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

    public async Task UpdateParkingLogAsync(ParkingLog parkingLog)
    {
        _context.Set<ParkingLog>().Update(parkingLog);
        await _context.SaveChangesAsync();
    }
}
