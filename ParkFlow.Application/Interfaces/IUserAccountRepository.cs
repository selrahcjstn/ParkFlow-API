namespace ParkFlow.Application.Interfaces
{
    public interface IUserAccountRepository
    {
        Task<UserAccount?> GetByEmailAsync(string email);
        Task<UserAccount?> GetByIdAsync(Guid id);

        Task AddAsync(UserAccount user);
        Task UpdateAsync(UserAccount user);
    }
}
