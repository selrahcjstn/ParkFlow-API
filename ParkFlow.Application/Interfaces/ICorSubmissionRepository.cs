namespace ParkFlow.Application.Interfaces;

public interface ICorSubmissionRepository
{
    async Task<IEnumerable<CorSubmission>> GetByUserIdsAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.ToHashSet();
        return (await ListCorSubmissionsAsync()).Where(cor => ids.Contains(cor.UserAccountId))
            .OrderByDescending(cor => cor.CreatedAt);
    }
    Task<CorSubmission?> GetCorSubmissionAsync(Guid id);
    Task<CorSubmission?> GetByUserIdAndTermAsync(Guid userAccountId, string academicTerm);
    Task<CorSubmission?> GetLatestByUserIdAsync(Guid userAccountId);
    Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync();
    Task AddCorSubmissionAsync(CorSubmission corSubmission);
    Task UpdateCorSubmissionAsync(CorSubmission corSubmission);
    Task DeleteCorSubmissionAsync(CorSubmission corSubmission);
}
