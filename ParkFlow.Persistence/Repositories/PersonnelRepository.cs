using Microsoft.EntityFrameworkCore;
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
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Personnel personnel)
    {
        _context.Personnel.Update(personnel);
        await _context.SaveChangesAsync();
    }

    public async Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId)
    {
        return await _context.Personnel
            .Include(p => p.UserProfile)
            .FirstOrDefaultAsync(x => x.UserProfileId == userProfileId);
    }

    public async Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber)
    {
        return await _context.Personnel
            .Include(p => p.UserProfile)
            .FirstOrDefaultAsync(x => x.IdCardNumber == idCardNumber);
    }
}
