using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IPersonnelRepository
{
    Task<Personnel?> GetByUserProfileIdAsync(Guid userProfileId);
    Task<Personnel?> GetByIdCardNumberAsync(string idCardNumber, Guid? excludeProfileId = null);
    Task<bool> IdCardNumberExistsAsync(string idCardNumber, Guid? excludeProfileId = null);
    Task AddAsync(Personnel personnel);
    Task UpdateAsync(Personnel personnel);
    Task DeleteAsync(Personnel personnel);
}
