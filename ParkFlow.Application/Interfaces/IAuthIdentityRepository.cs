using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Interfaces;

public interface IAuthIdentityRepository
{
    Task<AuthIdentity?> GetByProviderIdAsync(AuthProvider provider, string providerId);
    Task<AuthIdentity?> GetByEmailAsync(string email);
    Task<IEnumerable<AuthIdentity>> GetByAccountIdAsync(Guid accountId);
    Task AddAsync(AuthIdentity identity);
    Task UpdateAsync(AuthIdentity identity);
    Task DeleteAsync(AuthIdentity identity);
}
