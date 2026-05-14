using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Persistence.Repositories
{
    public class UserAccountRepository(AppDbContext appDbContext) : IUserAccountRepository
    {
        private readonly AppDbContext _appDbContext = appDbContext;

        public async Task AddAsync(UserAccount user)
        {
            await _appDbContext.UserAccounts.AddAsync(user);
            await _appDbContext.SaveChangesAsync();
        }

        public async Task<UserAccount?> GetByEmailAsync(string email)
        {
            return await _appDbContext.UserAccounts
                .AsNoTracking()
                .Include(u => u.UserProfile)
                    .ThenInclude(p => p.Student)
                .Include(u => u.UserProfile)
                    .ThenInclude(p => p.Personnel)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<UserAccount?> GetByIdAsync(Guid id)
        {
            return await _appDbContext.UserAccounts.FindAsync(id);
        }

        public async Task<string> GetProfileTypeByUserId(Guid userId)
        {
            var user = await _appDbContext.UserAccounts
                .Include(u => u.UserProfile)
                    .ThenInclude(p => p.Student)
                .Include(u => u.UserProfile)
                    .ThenInclude(p => p.Personnel)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.UserProfile?.Student != null)
                return "student";

            if (user?.UserProfile?.Personnel != null)
                return "personnel";

            throw new Exception("Invalid profile type");
        }

        public Task UpdateAsync(UserAccount user)
        {
            _appDbContext.UserAccounts.Update(user);
            return _appDbContext.SaveChangesAsync();
        }
    }
}
