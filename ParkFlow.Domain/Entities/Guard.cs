namespace ParkFlow.Domain.Entities;

public class Guard
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;

    public int AssignedGate { get; set; }
}
