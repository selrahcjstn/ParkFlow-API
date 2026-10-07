using ParkFlow.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ParkFlow.Persistence.Repositories;

public class CorSubmissionRepository : ICorSubmissionRepository
{
    private readonly AppDbContext _appDbContext;

    public CorSubmissionRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }
    
    public async Task AddCorSubmissionAsync(CorSubmission corSubmission)
    {
        await _appDbContext.CorSubmissions.AddAsync(corSubmission);
        await _appDbContext.SaveChangesAsync();
    }

    public async Task DeleteCorSubmissionAsync(CorSubmission corSubmission)
    {
        _appDbContext.CorSubmissions.Remove(corSubmission);
        await _appDbContext.SaveChangesAsync();
    }

    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id)
    {
        return _appDbContext.CorSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userAccountId, string academicTerm)
    {
        return _appDbContext.CorSubmissions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserAccountId == userAccountId && x.AcademicTerm == academicTerm);
    }

    public Task<CorSubmission?> GetLatestByUserIdAsync(Guid userAccountId)
    {
        return _appDbContext.CorSubmissions
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(x => x.UserAccountId == userAccountId);
    }

    public async Task<IEnumerable<CorSubmission>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        return await _appDbContext.CorSubmissions.AsNoTracking()
            .Where(cor => ids.Contains(cor.UserAccountId))
            .OrderByDescending(cor => cor.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync()
    {
        return await _appDbContext.CorSubmissions
            .Include(x => x.UserAccount)
                .ThenInclude(u => u.UserProfile)
                    .ThenInclude(p => p!.Personnel)
            .Include(x => x.UserAccount)
                .ThenInclude(u => u.AuthIdentities)
            .OrderByDescending(x => x.CreatedAt)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateCorSubmissionAsync(CorSubmission corSubmission)
    {
        var entry = _appDbContext.Entry(corSubmission);
        if (entry.State == EntityState.Detached)
        {
            _appDbContext.CorSubmissions.Update(corSubmission);
        }
        await _appDbContext.SaveChangesAsync();
    }
}
