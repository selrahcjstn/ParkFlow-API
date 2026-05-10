using ParkFlow.Application.Interfaces;

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

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId)
    {
        return await _appDbContext.UserProfiles.FindAsync(userId);
    }

    public async Task UpdateAsync(UserProfile profile)
    {
        _appDbContext.UserProfiles.Update(profile);
        await _appDbContext.SaveChangesAsync();
    }
}
