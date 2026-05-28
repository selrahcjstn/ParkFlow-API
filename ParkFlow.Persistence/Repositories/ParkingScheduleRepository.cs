using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ParkFlow.Persistence.Repositories;

public class ParkingScheduleRepository : IParkingScheduleRepository
{
    private readonly AppDbContext _appDbContext;

    public ParkingScheduleRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }

    public async Task AddAsync(ParkingSchedule parkingSchedule)
    {
        await _appDbContext.ParkingSchedules.AddAsync(parkingSchedule);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task DeleteAsync(ParkingSchedule parkingSchedule)
    {
        _appDbContext.ParkingSchedules.Remove(parkingSchedule);
        await _appDbContext.SaveChangesAsync();
    }

    public Task<ParkingSchedule?> GetByIdAsync(Guid id)
    {
        return _appDbContext.ParkingSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdAsync(Guid submissionId)
    {
        return await _appDbContext.ParkingSchedules
            .Include(x => x.CorSubmission)
            .AsNoTracking()
            .Where(x => x.SubmissionId == submissionId)
            .ToListAsync();
    }

    public async Task<IEnumerable<ParkingSchedule>> GetByUserIdAsync(Guid userId)
    {
        // 1. Resolve UserAccountId: check if the passed ID matches a UserProfile.Id first
        var targetUserAccountId = userId;
        var profile = await _appDbContext.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId);
        
        if (profile != null)
        {
            targetUserAccountId = profile.UserAccountId;
        }

        // 2. Fetch all COR submissions for this user account
        var submissions = await _appDbContext.CorSubmissions
            .AsNoTracking()
            .Where(c => c.UserAccountId == targetUserAccountId)
            .ToListAsync();

        if (!submissions.Any())
        {
            return Enumerable.Empty<ParkingSchedule>();
        }

        // 3. Fetch all schedules linked to those submissions
        var submissionIds = submissions.Select(c => c.Id).ToList();

        return await _appDbContext.ParkingSchedules
            .Include(x => x.CorSubmission)
            .AsNoTracking()
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .ToListAsync();
    }

    public async Task UpdateAsync(ParkingSchedule parkingSchedule)
    {
        _appDbContext.ParkingSchedules.Update(parkingSchedule);
        await _appDbContext.SaveChangesAsync();
    }
}
