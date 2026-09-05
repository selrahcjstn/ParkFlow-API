using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories
{
    public class SystemAnnouncementRepository : ISystemAnnouncementRepository
    {
        private readonly AppDbContext _context;

        public SystemAnnouncementRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SystemAnnouncement?> GetActiveAsync()
        {
            return await _context.SystemAnnouncements
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task AddAsync(SystemAnnouncement announcement)
        {
            await _context.SystemAnnouncements.AddAsync(announcement);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateAsync(SystemAnnouncement announcement)
        {
            _context.SystemAnnouncements.Update(announcement);
            await _context.SaveChangesAsync();
        }
    }
}
