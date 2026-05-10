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

    public async Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync()
    {
        return await _appDbContext.CorSubmissions
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateCorSubmissionAsync(CorSubmission corSubmission)
    {
        _appDbContext.CorSubmissions.Update(corSubmission);
        await _appDbContext.SaveChangesAsync();
    }
}
