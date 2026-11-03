using System;
using System.Threading;
using System.Threading.Tasks;

namespace ParkFlow.Application.Interfaces;

public interface IOverstayNotificationService
{
    Task<int> CheckAndSendOverstayNotificationsAsync(DateTime? checkTimeUtc = null, CancellationToken ct = default);
}
