using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class Admin
{
    public Guid UserProfileId { get; set; }  
    public UserProfile UserProfile { get; set; } = null!; 
    public RoleLevel RoleLevel { get; set; }

    private Admin() { }

    public Admin(UserProfile userProfile, RoleLevel roleLevel)
    {
        UserProfile = userProfile;
        UserProfileId = userProfile.Id;
        RoleLevel = roleLevel;
    }
}
