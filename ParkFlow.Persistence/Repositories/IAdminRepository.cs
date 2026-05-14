using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly AppDbContext _context;

    public AdminRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Admin admin)
    {
        await _context.Admins.AddAsync(admin);
        await _context.SaveChangesAsync();
    }

    public async Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Admins
            .Include(a => a.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }
}
