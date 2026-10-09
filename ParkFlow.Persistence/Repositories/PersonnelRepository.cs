using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkFlow.Application.Common;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class PersonnelRepository : IPersonnelRepository
{
    private readonly AppDbContext _context;

    public PersonnelRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Personnel personnel)
    {
        await _context.Personnel.AddAsync(personnel);
        await SaveIdChangesAsync();
    }

    public async Task UpdateAsync(Personnel personnel)
    {
        var entry = _context.Entry(personnel);
        if (entry.State == EntityState.Detached)
        {
            personnel.UserProfile = null!;
            _context.Personnel.Update(personnel);
        }
        await SaveIdChangesAsync();
    }

    public async Task DeleteAsync(Personnel personnel)
    {
        _context.Personnel.Remove(personnel);
        await _context.SaveChangesAsync();
    }

    private async Task SaveIdChangesAsync()
    {
        try { await _context.SaveChangesAsync(); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_Personnel_IdCardNumber" })
        {
            throw new RegistrationIdConflictException("This employee ID number is already registered. Please check your ID number or contact campus administration.", ex);
        }
    }

    public async Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Personnel
            .Include(p => p.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }

    public async Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber, Guid? excludeProfileId = null)
    {
        if (string.IsNullOrWhiteSpace(idCardNumber)) return null;
        var normalized = Personnel.NormalizeId(idCardNumber);
        return await _context.Personnel
            .Where(x => excludeProfileId == null || x.UserProfileId != excludeProfileId)
            .Include(p => p.UserProfile)
            .FirstOrDefaultAsync(x => x.IdCardNumber.Trim().ToUpper() == normalized);
    }
}
