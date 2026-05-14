namespace ParkFlow.Domain.Entities;

public class Personnel
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;
    public string Department { get; set; } = null!;
}
