using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Users.DTOs;

public class UpdateUserAccountRequest
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public Roles? Role { get; set; }
}
