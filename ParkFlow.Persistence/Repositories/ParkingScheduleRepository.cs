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

        // 2. Fetch all COR submissions for this user account ordered by creation descending
        var submissions = await _appDbContext.CorSubmissions
            .AsNoTracking()
            .Where(c => c.UserAccountId == targetUserAccountId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        if (!submissions.Any())
        {
            return Enumerable.Empty<ParkingSchedule>();
        }

        // Check if the latest submission has schedules
        var latestSubmission = submissions.First();
        var latestSchedules = await _appDbContext.ParkingSchedules
            .Include(x => x.CorSubmission)
            .AsNoTracking()
            .Where(x => x.SubmissionId == latestSubmission.Id)
            .ToListAsync();

        if (latestSchedules.Any())
        {
            return latestSchedules;
        }

        // 3. Fallback: Fetch all schedules linked to any of the user's submissions
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

    public async Task ReplaceSchedulesAsync(Guid submissionId, IEnumerable<ParkingSchedule> newSchedules)
    {
        var existing = await _appDbContext.ParkingSchedules
            .Where(x => x.SubmissionId == submissionId)
            .ToListAsync();

        if (existing.Count > 0)
        {
            _appDbContext.ParkingSchedules.RemoveRange(existing);
        }

        await _appDbContext.ParkingSchedules.AddRangeAsync(newSchedules);
        await _appDbContext.SaveChangesAsync();
    }
}
