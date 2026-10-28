using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly AppDbContext _appDbContext;
    private readonly ICacheService? _cache;

    public VehicleRepository(AppDbContext appDbContext, ICacheService? cache = null)
    {
        _appDbContext = appDbContext;
        _cache = cache;
    }

    public async Task AddAsync(Vehicle vehicle)
    {
        await VerificationMutation.RunAsync(_appDbContext, VerificationType.Vehicle, vehicle.Id, async () =>
        {
            await VehiclePlateGuard.EnsureRegisteredVehicleAsync(_appDbContext, vehicle);
            await _appDbContext.Set<Vehicle>().AddAsync(vehicle);
            await _appDbContext.SaveChangesAsync();
        }, ownerId: vehicle.OwnerId);
        await InvalidateAsync();
    }

    public async Task DeleteAsync(Vehicle vehicle)
    {
        await VerificationMutation.RunAsync(_appDbContext, VerificationType.Vehicle, vehicle.Id, async () =>
        {
            _appDbContext.Set<Vehicle>().Remove(vehicle);
            await _appDbContext.SaveChangesAsync();
        }, ownerId: vehicle.OwnerId);
        await InvalidateAsync();
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
        if (string.IsNullOrWhiteSpace(plateNumber)) return null;

        var normalized = plateNumber.Trim().ToLower();
        var clean = normalized.Replace(" ", "").Replace("-", "");

        // First try direct match
        var vehicle = await _appDbContext.Set<Vehicle>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PlateNumber.ToLower() == normalized);

        if (vehicle != null) return vehicle;

        // Fallback to match ignoring spaces and hyphens
        return await _appDbContext.Set<Vehicle>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PlateNumber.Replace(" ", "").Replace("-", "").ToLower() == clean);
    }

    public async Task<IEnumerable<Vehicle>> GetByOwnerIdAsync(Guid ownerId)
    {
        return await _appDbContext.Set<Vehicle>()
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
        await VerificationMutation.RunAsync(_appDbContext, VerificationType.Vehicle, vehicle.Id, async () =>
        {
            await VehiclePlateGuard.EnsureRegisteredVehicleAsync(_appDbContext, vehicle);
            var entry = _appDbContext.Entry(vehicle);
            if (entry.State == EntityState.Detached) _appDbContext.Set<Vehicle>().Update(vehicle);
            await _appDbContext.SaveChangesAsync();
        }, ownerId: vehicle.OwnerId);
        await InvalidateAsync();
    }
    private async Task InvalidateAsync()
    {
        if (_cache == null) return;
        await _cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.UserPrefix);
        await _cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.VehiclePrefix);
        await _cache.RemoveByPrefixAsync(ParkFlow.Application.Common.CacheKeys.DashboardPrefix);
    }
}
