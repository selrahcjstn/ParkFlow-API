namespace ParkFlow.Domain.Entities;

public class Guard
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;

     public ICollection<ParkingLog> ParkingLogs { get; set; } = [];
    public int AssignedGate { get; set; }
}
