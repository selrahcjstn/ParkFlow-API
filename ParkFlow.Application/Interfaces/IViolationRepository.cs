using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IViolationRepository
{
	Task AddAsync(Violation violation);
	Task<Violation?> GetByIdAsync(Guid id);
	Task<Violation?> GetByLogIdAsync(Guid logId);
	Task<IReadOnlyList<Violation>> GetRecentViolationsAsync(int limit);
	Task UpdateAsync(Violation violation);
	Task DeleteAsync(Violation violation);
}

