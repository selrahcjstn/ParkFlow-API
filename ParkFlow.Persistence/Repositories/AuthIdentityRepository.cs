using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

public class AuthIdentityRepository(AppDbContext appDbContext) : IAuthIdentityRepository
{
    private readonly AppDbContext _appDbContext = appDbContext;

    public async Task AddAsync(AuthIdentity identity)
    {
        await _appDbContext.AuthIdentities.AddAsync(identity);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task<AuthIdentity?> GetByProviderIdAsync(AuthProvider provider, string providerId)
    {
        return await _appDbContext.AuthIdentities
            .Include(i => i.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p.Student)
            .Include(i => i.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p.Personnel)
            .Include(i => i.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p.Guard)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Provider == provider && i.ProviderId == providerId);
    }

    public async Task<AuthIdentity?> GetByEmailAsync(string email)
    {
        return await _appDbContext.AuthIdentities
            .Include(i => i.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p.Student)

            .Include(i => i.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p.Personnel)

            .Include(i => i.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p.Guard)

            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Email != null && i.Email.ToLower() == email.ToLower());
    }

    public async Task<IEnumerable<AuthIdentity>> GetByAccountIdAsync(Guid accountId)
    {
        return await _appDbContext.AuthIdentities
            .AsNoTracking()
            .Where(i => i.UserAccountId == accountId)
            .ToListAsync();
    }

    public Task UpdateAsync(AuthIdentity identity)
    {
        _appDbContext.AuthIdentities.Update(identity);
        return _appDbContext.SaveChangesAsync();
    }

    public Task DeleteAsync(AuthIdentity identity)
    {
        _appDbContext.AuthIdentities.Remove(identity);
        return _appDbContext.SaveChangesAsync();
    }
}
