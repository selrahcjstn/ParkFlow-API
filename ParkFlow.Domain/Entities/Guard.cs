namespace ParkFlow.Domain.Entities;

public class Guard
{
    // PK + FK
    public Guid UserProfileId { get; private set; }

    public UserProfile UserProfile { get; private set; } = null!;

    public ICollection<ParkingLog> ParkingLogs { get; private set; }
        = new List<ParkingLog>();

    public int AssignedGate { get; private set; }

    private Guard() { }

    public Guard(
        Guid userProfileId,
        int assignedGate)
    {
        UserProfileId = userProfileId;
        AssignedGate = assignedGate;
    }

    public void ChangeAssignedGate(int assignedGate)
    {
        AssignedGate = assignedGate;
    }
}