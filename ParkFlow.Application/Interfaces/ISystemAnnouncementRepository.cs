using System.Threading.Tasks;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces
{
    public interface ISystemAnnouncementRepository
    {
        Task<SystemAnnouncement?> GetActiveAsync();
        Task AddAsync(SystemAnnouncement announcement);
        Task UpdateAsync(SystemAnnouncement announcement);
    }
}
