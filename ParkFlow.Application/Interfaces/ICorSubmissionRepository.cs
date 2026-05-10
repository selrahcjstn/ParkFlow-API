namespace ParkFlow.Application.Interfaces;

public interface ICorSubmissionRepository
{
    Task<CorSubmission?> GetCorSubmissionAsync(Guid id);
    Task<IEnumerable<CorSubmission>> ListCorSubmissionsAsync();
    Task AddCorSubmissionAsync(CorSubmission corSubmission);
    Task UpdateCorSubmissionAsync(CorSubmission corSubmission);
    Task DeleteCorSubmissionAsync(CorSubmission corSubmission);
}
