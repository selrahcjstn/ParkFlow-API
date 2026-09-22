using System;
using System.Threading.Tasks;
using ParkFlow.Application.Features.Notifications.DTOs;
using ParkFlow.Application.Interfaces;
using ParkFlow.Domain.Entities;

namespace ParkFlow.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly IUserNotificationRepository _repository;
    private readonly ISignalRNotificationSender _signalRNotificationSender;

    public NotificationService(
        IUserNotificationRepository repository,
        ISignalRNotificationSender signalRNotificationSender)
    {
        _repository = repository;
        _signalRNotificationSender = signalRNotificationSender;
    }

    public async Task CreateAndSendNotificationAsync(
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
        object? signalRData = null)
    {
        if (userAccountId == Guid.Empty) return;

        var entity = new UserNotification(
            userAccountId,
            title,
            body,
            type,
            subtitle,
            referenceCode,
            vehiclePlate,
            actionRoute,
            actionText,
            priority,
            issuer,
            driverName,
            driverRole,
            vehicleBrand);

        await _repository.AddAsync(entity);

        try
        {
            var dto = UserNotificationDto.FromEntity(entity);
            var payload = signalRData ?? dto;
            await _signalRNotificationSender.SendToUserAsync(userAccountId.ToString(), "ReceiveNotification", dto);
            
            if (type == "overdue")
            {
                await _signalRNotificationSender.SendToUserAsync(userAccountId.ToString(), "ReceiveViolation", payload);
            }
            else if (type == "approved" && (actionRoute?.Contains("history") == true || title.Contains("Exit")))
            {
                await _signalRNotificationSender.SendToUserAsync(userAccountId.ToString(), "ExitResponse", payload);
            }
        }
        catch
        {
            // Ignore SignalR dispatch failure if user is offline
        }
    }
}
