using System;
using System.Threading.Tasks;

namespace ParkFlow.Application.Interfaces;

public interface INotificationService
{
    Task CreateAndSendNotificationAsync(
        Guid userAccountId,
        string title,
        string body,
        string type = "system",
        string? subtitle = null,
        string? referenceCode = null,
        string? vehiclePlate = null,
        string? actionRoute = null,
        string? actionText = null,
        string? priority = null,
        string? issuer = null,
        string? driverName = null,
        string? driverRole = null,
        string? vehicleBrand = null,
        object? signalRData = null);
}
