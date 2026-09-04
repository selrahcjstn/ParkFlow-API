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
            .Include(u => u.UserProfile)
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

    public async Task<bool> DeleteAsync(Guid id)
    {
        var user = await _appDbContext.UserAccounts.FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return false;

        // 1. Find UserProfile & child entities (Student, Personnel, Guard)
        var profile = await _appDbContext.UserProfiles
            .Include(p => p.Student)
            .Include(p => p.Personnel)
            .Include(p => p.Guard)
            .FirstOrDefaultAsync(p => p.UserAccountId == id);

        // 2. Find all Vehicles owned by this user
        var vehicles = await _appDbContext.Vehicles.Where(v => v.OwnerId == id).ToListAsync();
        var vehicleIds = vehicles.Select(v => v.Id).ToList();

        // 3. Find all ParkingLogs linked to user's vehicles or guard profile
        var logQuery = _appDbContext.ParkingLogs.Where(p => vehicleIds.Contains(p.VehicleId));
        if (profile?.Guard != null)
        {
            var guardProfileId = profile.Guard.UserProfileId;
            logQuery = _appDbContext.ParkingLogs.Where(p => vehicleIds.Contains(p.VehicleId) || p.GuardId == guardProfileId);
        }

        var logs = await logQuery.ToListAsync();
        var logIds = logs.Select(l => l.Id).ToList();

        // 4. Delete Violations linked to those parking logs
        if (logIds.Count > 0)
        {
            var violations = await _appDbContext.Violations.Where(v => logIds.Contains(v.ParkingLogId)).ToListAsync();
            _appDbContext.Violations.RemoveRange(violations);
        }

        // 5. Delete ParkingLogs & Vehicles
        if (logs.Count > 0) _appDbContext.ParkingLogs.RemoveRange(logs);
        if (vehicles.Count > 0) _appDbContext.Vehicles.RemoveRange(vehicles);

        // 6. Delete CorSubmissions & ParkingSchedules
        var corSubs = await _appDbContext.CorSubmissions.Where(c => c.UserAccountId == id).ToListAsync();
        if (corSubs.Count > 0)
        {
            var corSubIds = corSubs.Select(c => c.Id).ToList();
            var schedules = await _appDbContext.ParkingSchedules.Where(s => corSubIds.Contains(s.SubmissionId)).ToListAsync();
            if (schedules.Count > 0) _appDbContext.ParkingSchedules.RemoveRange(schedules);
            _appDbContext.CorSubmissions.RemoveRange(corSubs);
        }

        // 7. Delete AuthIdentities & PasswordHistories
        var identities = await _appDbContext.AuthIdentities.Where(a => a.UserAccountId == id).ToListAsync();
        var emails = identities.Where(i => i.Email != null).Select(i => i.Email!.ToLower()).ToList();

        if (identities.Count > 0) _appDbContext.AuthIdentities.RemoveRange(identities);

        var passHistories = await _appDbContext.PasswordHistories.Where(p => p.UserAccountId == id).ToListAsync();
        if (passHistories.Count > 0) _appDbContext.PasswordHistories.RemoveRange(passHistories);

        // 8. Delete EmailOtps
        if (emails.Count > 0)
        {
            var otps = await _appDbContext.EmailOtps.Where(o => emails.Contains(o.Email.ToLower())).ToListAsync();
            if (otps.Count > 0) _appDbContext.EmailOtps.RemoveRange(otps);
        }

        // 9. Delete Admins entry if any
        if (profile != null)
        {
            var admins = await _appDbContext.Admins.Where(a => a.UserProfileId == profile.Id).ToListAsync();
            if (admins.Count > 0) _appDbContext.Admins.RemoveRange(admins);
        }

        // 10. Delete Profile and child Student / Personnel / Guard
        if (profile != null)
        {
            if (profile.Student != null) _appDbContext.Students.Remove(profile.Student);
            if (profile.Personnel != null) _appDbContext.Personnel.Remove(profile.Personnel);
            if (profile.Guard != null) _appDbContext.Guards.Remove(profile.Guard);
            _appDbContext.UserProfiles.Remove(profile);
        }

        // 11. Delete UserAccount
        _appDbContext.UserAccounts.Remove(user);

        await _appDbContext.SaveChangesAsync();
        return true;
    }
}
