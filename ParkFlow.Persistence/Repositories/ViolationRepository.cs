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

    public async Task UpdateAsync(Violation violation)
    {
        _context.Set<Violation>().Update(violation);
        await _context.SaveChangesAsync();
    }
}
