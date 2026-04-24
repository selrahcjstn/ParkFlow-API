using ParkFlow.Application.Interfaces;

namespace ParkFlow.Persistence.Repositories
{
    public class UserAccountRepository : IUserAccountRepository
    {
        private readonly AppDbContext _appDbContext;

        public UserAccountRepository(AppDbContext appDbContext)
        {
            _appDbContext = appDbContext;
        }

        public async Task AddAsync(UserAccount user)
        {
            await _appDbContext.UserAccounts.AddAsync(user);
            await _appDbContext.SaveChangesAsync();
        }

        public Task<UserAccount?> GetByEmailAsync(string email)
        {
            throw new NotImplementedException();
        }

        public Task<UserAccount?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task SaveChangesAsync()
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(UserAccount user)
        {
            throw new NotImplementedException();
        }
    }
}
