namespace ParkFlow.Domain.Entities;

public class Personnel
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;
    public string IdCardNumber { get; set; } = null!;
    public string Department { get; set; } = null!;

    private Personnel() { }

    public Personnel(
        Guid profileId,
        string idCardNumber,
        string department)
    {                                                   
        UserProfileId = profileId;
        IdCardNumber = idCardNumber;
        Department = department;
    }

    public void UpdateDetails(string idCardNumber, string department)
    {
        IdCardNumber = idCardNumber;
        Department = department;
    }
}
