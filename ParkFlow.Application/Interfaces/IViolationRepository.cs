using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IViolationRepository
{
	Task AddAsync(Violation violation);
	Task<Violation?> GetByIdAsync(Guid id);
	Task<Violation?> GetByLogIdAsync(Guid logId);
	Task<Violation?> GetByReferenceNumberAsync(string referenceNumber);
	Task<IReadOnlyList<Violation>> GetRecentViolationsAsync(int limit);
	Task<IReadOnlyList<Violation>> GetViolationHistoryAsync(Guid? userId = null, int pageNumber = 1, int pageSize = 15);
	Task UpdateAsync(Violation violation);
	Task DeleteAsync(Violation violation);
}

