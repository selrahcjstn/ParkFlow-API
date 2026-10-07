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

    public async Task<IEnumerable<ParkingSchedule>> GetBySubmissionIdsAsync(IEnumerable<Guid> submissionIds)
    {
        var ids = submissionIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await _appDbContext.ParkingSchedules.AsNoTracking()
            .Where(schedule => ids.Contains(schedule.SubmissionId)).ToListAsync();
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

        var submissionIds = submissions.Select(c => c.Id).ToList();

        // Check latest submission schedules first
        var latestSubmissionId = submissions.First().Id;
        var latestSchedules = await _appDbContext.ParkingSchedules
            .Include(x => x.CorSubmission)
            .AsNoTracking()
            .Where(x => x.SubmissionId == latestSubmissionId)
            .OrderBy(x => x.DayOfWeek)
            .ToListAsync();

        if (latestSchedules.Any())
        {
            return latestSchedules;
        }

        // Fallback: Fetch all schedules across any user submission, pick latest per DayOfWeek
        var allUserSchedules = await _appDbContext.ParkingSchedules
            .Include(x => x.CorSubmission)
            .AsNoTracking()
            .Where(x => submissionIds.Contains(x.SubmissionId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return allUserSchedules
            .GroupBy(s => s.DayOfWeek)
            .Select(g => g.First())
            .OrderBy(s => s.DayOfWeek)
            .ToList();
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
