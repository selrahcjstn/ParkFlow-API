using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Application.Interfaces;

public interface IUserNotificationRepository
{
    Task AddAsync(UserNotification notification);
    Task<UserNotification?> GetByIdAsync(Guid id);
    Task<IEnumerable<UserNotification>> GetByUserIdAsync(Guid userId, int limit = 50);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task UpdateAsync(UserNotification notification);
    Task MarkAllAsReadForUserAsync(Guid userId);
    Task DeleteAsync(UserNotification notification);
}
