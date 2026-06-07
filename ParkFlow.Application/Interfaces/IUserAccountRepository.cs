using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Interfaces;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByEmailAsync(string email);
    Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider authProvider, string externalProviderId);
    Task<UserAccount?> GetByIdAsync(Guid id);
    Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber);
    Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null);

    Task AddAsync(UserAccount user);
    Task UpdateAsync(UserAccount user);
}
