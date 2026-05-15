namespace ParkFlow.Domain.Entities;

public class Guard
{
    public Guid UserProfileId { get; set; }   // FK + PK
    public UserProfile UserProfile { get; set; } = null!;

    public ICollection<ParkingLog> ParkingLogs { get; set; }
        = new List<ParkingLog>();

    public int AssignedGate { get; set; }

    private Guard() { }

    public Guard(UserProfile userProfile, int assignedGate)
    {
        UserProfile = userProfile;
        UserProfileId = userProfile.Id;
        AssignedGate = assignedGate;
    }

    public void ChangeAssignedGate(int assignedGate)
    {
        AssignedGate = assignedGate;
    }
}