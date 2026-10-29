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
    Task SaveEmployeeScheduleAsync(CorSubmission submission) => throw new NotSupportedException("Separate employee schedule persistence is required.");
    // Commit the uploaded source and extraction state together so workers respect
    // the explicit Fetch Information action and any reusable extraction.
    Task SaveUploadedAsync(CorSubmission submission, bool create, ParkFlow.Application.Features.Verification.CorFields? fields,
        string? outcome, CancellationToken ct) => create ? AddCorSubmissionAsync(submission) : UpdateCorSubmissionAsync(submission);
    Task DeleteCorSubmissionAsync(CorSubmission corSubmission);
}
