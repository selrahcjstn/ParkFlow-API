using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class ViolationRepository : IViolationRepository
{
    private readonly AppDbContext _context;

    public ViolationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Violation violation)
    {
        await _context.Set<Violation>().AddAsync(violation);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Violation violation)
    {
        _context.Set<Violation>().Remove(violation);
        await _context.SaveChangesAsync();
    }

    public async Task<Violation?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Violation>().FindAsync(id).AsTask();
    }

    public async Task<Violation?> GetByLogIdAsync(Guid logId)
    {
        try
        {
            return await _context.Set<Violation>()
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.ParkingLogId == logId);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return null;
        }
    }

    public async Task<Violation?> GetByReferenceNumberAsync(string referenceNumber)
    {
        try
        {
            return await _context.Set<Violation>()
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Student)
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Personnel)
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Guard)
                .FirstOrDefaultAsync(v => v.ReferenceNumber == referenceNumber);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Violation>> GetRecentViolationsAsync(int limit)
    {
        try
        {
            return await _context.Set<Violation>()
                .AsNoTracking()
                .OrderByDescending(v => v.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Violation>> GetViolationHistoryAsync(
        Guid? userId = null,
        int pageNumber = 1,
        int pageSize = 15)
    {
        try
        {
            var query = _context.Set<Violation>()
                .AsNoTracking()
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Student)
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Personnel)
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Guard)
                .AsQueryable();

            if (userId.HasValue)
                query = query.Where(v => v.ParkingLog.Vehicle.OwnerId == userId.Value);

            return await query
                .OrderByDescending(v => v.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    public async Task<IReadOnlyList<Violation>> GetViolationsByUserIdAsync(Guid userId)
    {
        try
        {
            return await _context.Set<Violation>()
                .AsNoTracking()
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Student)
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Personnel)
                .Include(v => v.ParkingLog)
                    .ThenInclude(pl => pl.Vehicle)
                        .ThenInclude(ve => ve.Owner)
                            .ThenInclude(ua => ua.UserProfile)
                                .ThenInclude(up => up!.Guard)
                .Where(v => v.ParkingLog.Vehicle.OwnerId == userId)
                .OrderByDescending(v => v.CreatedAt)
                .ToListAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return [];
        }
    }

    public async Task<bool> HasActiveViolationAsync(Guid vehicleId)
    {
        try
        {
            return await _context.Set<Violation>()
                .AnyAsync(v => v.ParkingLog.VehicleId == vehicleId && v.SettlementStatus != global::SettlementStatus.Settled);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            return false;
        }
    }

    public async Task UpdateAsync(Violation violation)
    {
        _context.Set<Violation>().Update(violation);
        await _context.SaveChangesAsync();
    }
}
