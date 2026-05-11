using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ParkFlow.Application.Interfaces;

namespace ParkFlow.Infrastructure.Security;

public class UserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid GetUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var id = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                 ?? user?.FindFirst("sub")?.Value;

        return Guid.TryParse(id, out var guid) ? guid : Guid.Empty;
    }
}
