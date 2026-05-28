using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IPersonnelRepository
{
    Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId);
    Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber);
    Task AddAsync(Personnel personnel);
    Task UpdateAsync(Personnel personnel);
}
