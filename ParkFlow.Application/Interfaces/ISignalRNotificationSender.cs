namespace ParkFlow.Application.Interfaces
{
    public interface ISignalRNotificationSender
    {
        Task SendEventNotificationAsync(string userId, object data);
    }
}
