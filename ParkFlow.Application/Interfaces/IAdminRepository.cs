using System.Collections.Generic;
using System.Threading.Tasks;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IAdminRepository
{
    Task<Admin?> GetByUserProfileIdAsync(Guid userProfileId);
    Task<IEnumerable<Admin>> ListAllAsync();
    Task AddAsync(Admin admin);
}
