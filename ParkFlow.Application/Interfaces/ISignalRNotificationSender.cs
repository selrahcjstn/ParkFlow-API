namespace ParkFlow.Application.Interfaces
{
    public interface ISignalRNotificationSender
    {
        Task SendEventNotificationAsync(string userId, object data);
        Task SendToUserAsync(string userId, string method, object data);
        Task SendToAllAsync(string method, object data);
    }
}
