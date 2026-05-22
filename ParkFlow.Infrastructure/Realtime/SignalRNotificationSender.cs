using Microsoft.AspNetCore.SignalR;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure.Realtime;

public class SignalRNotificationSender : ISignalRNotificationSender
{
    private readonly IHubContext<NotificationHub> _hub;

    public SignalRNotificationSender(IHubContext<NotificationHub> hub)
    {
        _hub = hub;
    }

    public Task SendEventNotificationAsync(string userId, object data)
    {
        return _hub.Clients.User(userId)
             .SendAsync("ExitResponse", data);
    }
}