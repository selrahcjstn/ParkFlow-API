using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IPersonnelRepository
{
    Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId);
    Task AddAsync(Personnel personnel);
}
