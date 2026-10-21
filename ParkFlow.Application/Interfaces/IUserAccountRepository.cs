using System.Collections.Generic;
using System.Threading.Tasks;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Interfaces;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByEmailAsync(string email);
    Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider authProvider, string externalProviderId);
    Task<UserAccount?> GetByIdAsync(Guid id);
    async Task<bool> ExistsAsync(Guid id) => await GetByIdAsync(id) != null;
    Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null);
    Task<IEnumerable<UserAccount>> ListAllAsync();

    Task AddAsync(UserAccount user);
    Task UpdateAsync(UserAccount user);
    Task<bool> DeleteAsync(Guid id);
}
