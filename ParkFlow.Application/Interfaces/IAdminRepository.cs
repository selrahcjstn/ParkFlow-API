using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IAdminRepository
{
    Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId);
    Task AddAsync(Admin admin);
}
