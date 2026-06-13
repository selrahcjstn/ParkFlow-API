using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Enums;

namespace ParkFlow.Persistence.Repositories;

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
            .Include(u => u.UserProfile)
                .ThenInclude(p => p!.Student)
            .Include(u => u.UserProfile)
                .ThenInclude(p => p!.Personnel)
            .Include(u => u.UserProfile)
                .ThenInclude(p => p!.Guard)
            .Include(u => u.AuthIdentities)
            .Include(u => u.PasswordHistories)
            .FirstOrDefaultAsync(u => u.AuthIdentities.Any(i => i.Email != null && i.Email.ToLower() == email.ToLower()));
    }

    public async Task<UserAccount?> GetByAuthProviderExternalIdAsync(AuthProvider authProvider, string externalProviderId)
    {
        return await _appDbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.AuthIdentities)
            .Include(u => u.PasswordHistories)
            .FirstOrDefaultAsync(u =>
                u.AuthProvider == authProvider &&
                u.ExternalProviderId == externalProviderId);
    }

    public async Task<UserAccount?> GetByIdAsync(Guid id)
    {
        return await _appDbContext.UserAccounts
            .Include(u => u.AuthIdentities)
            .Include(u => u.PasswordHistories)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<UserAccount?> GetByPhoneNumberAsync(string phoneNumber)
    {
        return await _appDbContext.UserAccounts
            .Include(u => u.UserProfile)
            .FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber);
    }

    public async Task<bool> EmailExistsAsync(string email, Guid? excludeUserId = null)
    {
        var query = _appDbContext.AuthIdentities
            .AsNoTracking()
            .Where(i => i.Email != null && i.Email.ToLower() == email.ToLower());

        if (excludeUserId.HasValue)
            query = query.Where(i => i.UserAccountId != excludeUserId.Value);

        return await query.AnyAsync();
    }

    public async Task<IEnumerable<UserAccount>> ListAllAsync()
    {
        return await _appDbContext.UserAccounts
            .Include(u => u.UserProfile)
                .ThenInclude(p => p!.Student)
            .Include(u => u.UserProfile)
                .ThenInclude(p => p!.Personnel)
            .Include(u => u.UserProfile)
                .ThenInclude(p => p!.Guard)
            .Include(u => u.AuthIdentities)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateAsync(UserAccount user)
    {
        await _appDbContext.SaveChangesAsync();
    }
}
