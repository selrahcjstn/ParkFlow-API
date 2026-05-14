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
        await _context.Guards.AddAsync(guard);
        await _context.SaveChangesAsync();
    }

    public async Task<Guard?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Guards
            .Include(g => g.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }
}
