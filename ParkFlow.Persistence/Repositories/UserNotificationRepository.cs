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

    // Hide retired financial messages until the separately approved purge is applied.
    private IQueryable<UserNotification> VisibleNotifications => _context.UserNotifications.Where(n =>
        n.Type.ToLower() != "violation" && n.Type.ToLower() != "payment" && n.Type.ToLower() != "payment_processed" && n.Type.ToLower() != "settlement" && n.Type.ToLower() != "invoice" &&
        (n.ActionRoute == null || (!n.ActionRoute.ToLower().Contains("violation") &&
            !n.ActionRoute.ToLower().Contains("process-payment") && !n.ActionRoute.ToLower().Contains("scan-receipt"))) &&
        (n.ReferenceCode == null || (!n.ReferenceCode.ToUpper().StartsWith("VIO-") && !n.ReferenceCode.ToUpper().StartsWith("PAY-") && !n.ReferenceCode.ToUpper().StartsWith("INV-"))) &&
        !n.Title.ToLower().Contains("payment") && !n.Title.ToLower().Contains("paid") &&
        !n.Title.ToLower().Contains("settled") && !n.Title.ToLower().Contains("violation") && !n.Title.ToLower().Contains("penalty") && !n.Title.ToLower().Contains("invoice") &&
        !n.Body.ToLower().Contains("payment") && !n.Body.ToLower().Contains("penalty") &&
        !n.Body.ToLower().Contains("charge") && !n.Body.ToLower().Contains("settlement") &&
        !n.Body.ToLower().Contains("invoice") && !n.Body.ToLower().Contains(" fee") && !n.Body.Contains("₱"));

    public async Task AddAsync(UserNotification notification)
    {
        await _context.UserNotifications.AddAsync(notification);
        await _context.SaveChangesAsync();
    }

    public async Task<UserNotification?> GetByIdAsync(Guid id)
    {
        return await VisibleNotifications.FirstOrDefaultAsync(n => n.Id == id);
    }

    public async Task<IEnumerable<UserNotification>> GetByUserIdAsync(Guid userId, int limit = 20, int skip = 0)
    {
        return await VisibleNotifications
            .AsNoTracking()
            .Where(n => n.UserAccountId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await VisibleNotifications
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
        var unread = await VisibleNotifications
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
