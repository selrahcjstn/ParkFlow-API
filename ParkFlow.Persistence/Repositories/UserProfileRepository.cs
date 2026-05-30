using ParkFlow.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ParkFlow.Persistence.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _appDbContext;

    public UserProfileRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task AddAsync(UserProfile profile)
    {
        await _appDbContext.UserProfiles.AddAsync(profile);
        await _appDbContext.SaveChangesAsync();
    }

    public Task<UserProfile?> GetByIdAsync(Guid id)
    {
        return _appDbContext.UserProfiles
            .Include(p => p.UserAccount)
            .Include(p => p.Student)
            .Include(p => p.Personnel)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId)
    {
        return await _appDbContext.UserProfiles
            .Include(p => p.UserAccount)
            .Include(p => p.Student)
            .Include(p => p.Personnel)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserAccountId == userId);
    }

    public async Task UpdateAsync(UserProfile profile)
    {
        profile.UserAccount = null!;
        _appDbContext.UserProfiles.Update(profile);
        await _appDbContext.SaveChangesAsync();
    }
}
