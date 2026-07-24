using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _appDbContext;

    public VehicleRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task AddAsync(Vehicle vehicle)
    {
        await _appDbContext.Set<Vehicle>().AddAsync(vehicle);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(Vehicle vehicle)
    {
        _appDbContext.Set<Vehicle>().Remove(vehicle);
        await _appDbContext.SaveChangesAsync();
    }

    public Task<Vehicle?> GetByIdAsync(Guid id)
    {
        return _appDbContext.Set<Vehicle>().FindAsync(id).AsTask();
    }

    public async Task<Vehicle?> GetByQrCodeHashAsync(string qrCodeHash)
    {
        return await _appDbContext.Set<Vehicle>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.QrCodeHash == qrCodeHash);
    }

    public async Task<Vehicle?> GetByPlateNumberAsync(string plateNumber)
    {
        return await _appDbContext.Set<Vehicle>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PlateNumber.ToLower() == plateNumber.ToLower());
    }

    public async Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId)
    {
        return await _appDbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(v => v.OwnerId == ownerId)
            .ToListAsync();
    }

    public async Task<IEnumerable<Vehicle>> GetByOwnerIdsAsync(IEnumerable<Guid> ownerIds)
    {
        return await _appDbContext.Set<Vehicle>()
            .AsNoTracking()
            .Where(v => ownerIds.Contains(v.OwnerId))
            .ToListAsync();
    }

    public async Task<IEnumerable<Vehicle>> GetAllAsync()
    {
        return await _appDbContext.Set<Vehicle>()
            .Include(v => v.Owner)
                .ThenInclude(o => o.AuthIdentities)
            .Include(v => v.Owner)
                .ThenInclude(o => o.UserProfile)
                    .ThenInclude(p => p!.Student)
            .Include(v => v.Owner)
                .ThenInclude(o => o.UserProfile)
                    .ThenInclude(p => p!.Personnel)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateAsync(Vehicle vehicle)
    {
        _appDbContext.Set<Vehicle>().Update(vehicle);
        await _appDbContext.SaveChangesAsync();
    }
}
