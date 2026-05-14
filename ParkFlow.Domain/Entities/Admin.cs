using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class Admin
{
    public Guid UserProfileId { get; set; }  
    public UserProfile UserProfile { get; set; } = null!; 
    public RoleLevel RoleLevel { get; set; }
}
