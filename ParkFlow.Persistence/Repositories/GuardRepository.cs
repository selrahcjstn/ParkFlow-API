using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class GuardRepository : IGuardRepository
{
    private readonly AppDbContext _context;

    public GuardRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Guard guard)
    {
        if (guard.UserProfile != null)
        {
            var profileEntry = _context.Entry(guard.UserProfile);
            if (profileEntry.State == EntityState.Added || profileEntry.State == EntityState.Detached)
            {
                profileEntry.State = EntityState.Unchanged;
            }
        }
        await _context.Guards.AddAsync(guard);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Guard guard)
    {
        var entry = _context.Entry(guard);
        if (entry.State == EntityState.Detached)
        {
            guard.UserProfile = null!;
            _context.Guards.Update(guard);
        }
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Guard guard)
    {
        _context.Guards.Remove(guard);
        await _context.SaveChangesAsync();
    }

    public async Task<Guard?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Guards
            .Include(g => g.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }
}
