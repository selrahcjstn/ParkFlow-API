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
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<UserAccount?> GetByIdAsync(Guid id)
        {
            return await _appDbContext.UserAccounts.FindAsync(id);
        }

        public Task UpdateAsync(UserAccount user)
        {
            _appDbContext.UserAccounts.Update(user);
            return _appDbContext.SaveChangesAsync();
        }
    }
}
