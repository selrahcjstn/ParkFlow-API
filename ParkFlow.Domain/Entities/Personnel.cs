using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class Personnel
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;
    public string IdCardNumber { get; set; } = null!;
    public string Department { get; set; } = null!;
    public Roles Role { get; set; } = Roles.UniversityStaff;

    private Personnel() { }

    public Personnel(
        Guid profileId,
        string idCardNumber,
        string department,
        Roles role = Roles.UniversityStaff)
    {                                                   
        UserProfileId = profileId;
        IdCardNumber = NormalizeId(idCardNumber);
        Department = department;
        Role = role;
    }

    public void UpdateDetails(string idCardNumber, string department, Roles? role = null)
    {
        IdCardNumber = NormalizeId(idCardNumber);
        Department = department;
        if (role.HasValue)
            Role = role.Value;
    }
    public static string NormalizeId(string idCardNumber) => idCardNumber.Trim().ToUpperInvariant();
}

