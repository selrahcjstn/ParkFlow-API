using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IGuardRepository
{
    Task<Guard?> GetByUserProfileIdAsync(Guid userProfileId);
    Task AddAsync(Guard guard);
}
