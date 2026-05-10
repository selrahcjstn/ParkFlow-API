using ParkFlow.Application.Interfaces;

namespace ParkFlow.Persistence.Repositories;

public class CorSubmissionRepository : ICorSubmissionRepository
{
    private readonly AppDbContext _appDbContext;

    public CorSubmissionRepository(AppDbContext appDbContext)
    {
        _appDbContext = appDbContext;
    }
    
    public Task AddCorSubmissionAsync(CorSubmission corSubmission)
    {
        throw new NotImplementedException();
    }

    public Task DeleteCorSubmissionAsync(CorSubmission corSubmission)
    {
        throw new NotImplementedException();
    }

    public Task<CorSubmission?> GetCorSubmissionAsync(Guid id)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync()
    {
        throw new NotImplementedException();
    }

    public Task UpdateCorSubmissionAsync(CorSubmission corSubmission)
    {
        throw new NotImplementedException();
    }
}
