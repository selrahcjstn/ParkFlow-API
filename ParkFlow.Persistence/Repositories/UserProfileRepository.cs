using ParkFlow.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        await VerificationMutation.RunAsync(_appDbContext, ParkFlow.Domain.Entities.VerificationType.Account, profile.UserAccountId, async () =>
        {
            await _appDbContext.UserProfiles.AddAsync(profile);
            await _appDbContext.SaveChangesAsync();
        });
    }

    public Task<UserProfile?> GetByIdAsync(Guid id)
    {
        return _appDbContext.UserProfiles
            .Include(p => p.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .Include(p => p.Student)
            .Include(p => p.Personnel)
            .Include(p => p.Guard)
            .Include(p => p.Admin)
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<UserProfile?> GetByUserIdAsync(Guid userId)
    {
        return await _appDbContext.UserProfiles
            .Include(p => p.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .Include(p => p.Student)
            .Include(p => p.Personnel)
            .Include(p => p.Guard)
            .Include(p => p.Admin)
            .FirstOrDefaultAsync(p => p.UserAccountId == userId);
    }

    public async Task UpdateAsync(UserProfile profile)
    {
        await VerificationMutation.RunAsync(_appDbContext, ParkFlow.Domain.Entities.VerificationType.Account, profile.UserAccountId, async () =>
        {
            var entry = _appDbContext.Entry(profile);
            if (entry.State == EntityState.Detached)
            {
                profile.UserAccount = null!;
                profile.Student = null!;
                profile.Personnel = null!;
                profile.Guard = null!;
                profile.Admin = null!;
                _appDbContext.UserProfiles.Update(profile);
            }
            await _appDbContext.SaveChangesAsync();
        });
    }

    public async Task<List<UserProfile>> SearchByNameOrPhoneAsync(string query, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var term = query.Trim().ToLower();
        return await _appDbContext.UserProfiles
            .Include(p => p.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .Include(p => p.Student)
            .Include(p => p.Personnel)
            .Include(p => p.Guard)
            .Include(p => p.Admin)
            .AsNoTracking()
            .Where(p =>
                (p.FirstName.ToLower() + " " + p.LastName.ToLower()).Contains(term) ||
                (p.LastName.ToLower() + " " + p.FirstName.ToLower()).Contains(term) ||
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                (p.UserAccount.PhoneNumber != null && p.UserAccount.PhoneNumber.Contains(term)) ||
                (p.Student != null && p.Student.StudentNumber.Contains(term)) ||
                (p.Personnel != null && p.Personnel.IdCardNumber.Contains(term)))
            .Take(limit)
            .ToListAsync();
    }
}

