using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Persistence.Repositories;

public class UserNotificationRepository : IUserNotificationRepository
{
    private readonly AppDbContext _context;

    public UserNotificationRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(UserNotification notification)
    {
        await _context.UserNotifications.AddAsync(notification);
        await _context.SaveChangesAsync();
    }

    public async Task<UserNotification?> GetByIdAsync(Guid id)
    {
        return await _context.UserNotifications.FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<IEnumerable<UserNotification>> GetByUserIdAsync(Guid userId, int limit = 50)
    {
        return await _context.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserAccountId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _context.UserNotifications
            .AsNoTracking()
            .CountAsync(n => n.UserAccountId == userId && !n.IsRead);
    }

    public async Task UpdateAsync(UserNotification notification)
    {
        _context.UserNotifications.Update(notification);
        await _context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadForUserAsync(Guid userId)
    {
        var unread = await _context.UserNotifications
            .Where(n => n.UserAccountId == userId && !n.IsRead)
            .ToListAsync();

        if (unread.Count > 0)
        {
            var now = DateTime.UtcNow;
            foreach (var item in unread)
            {
                item.MarkAsRead();
            }
            await _context.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(UserNotification notification)
    {
        _context.UserNotifications.Remove(notification);
        await _context.SaveChangesAsync();
    }
}
