namespace ParkFlow.Domain.Entities;

public class ParkingLog : BaseEntity
{
    public Guid VehicleId { get; private set; }
    public Vehicle Vehicle { get; private set; } = null!;

    public Guid  GuardId { get; private set; }
    public Guard Guard { get; private set; } = null!;

    public DateTime EntryTime { get; private set; }
    public DateTime? ExitTime { get; private set; }
    public ParkingStatus Status { get; private set; }

    private ParkingLog() { }

    public ParkingLog(
        Vehicle vehicle,
         Guard guard,
        DateTime entryTime,
        ParkingStatus status)
    {
        // Vehicle ID
        Vehicle = vehicle;
        VehicleId = vehicle.Id;

        // Guard ID
        Guard = guard;
        GuardId = guard.UserProfileId;

        EntryTime = entryTime;
        Status = status;
    }

    public void Exit(DateTime exitTime)
    {
        ExitTime = exitTime;
        Status = ParkingStatus.Exited;
        UpdatedAt = DateTime.UtcNow;
    }

}
