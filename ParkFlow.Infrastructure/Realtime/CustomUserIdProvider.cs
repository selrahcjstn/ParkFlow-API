using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace ParkFlow.Infrastructure.Realtime;

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? connection.User?.FindFirst("user_id")?.Value
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}
