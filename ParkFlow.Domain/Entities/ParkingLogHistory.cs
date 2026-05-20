using ParkFlow.Domain.Entities;

public class ParkingLogHistory : BaseEntity
{
    public Guid ParkingLogId { get; private set; }

    public Guid VehicleId { get; private set; }

    public Guid GuardId { get; private set; }

    public DateTime EntryTime { get; private set; }

    public DateTime ExitTime { get; private set; }


    private ParkingLogHistory() { }

    public ParkingLogHistory(ParkingLog log)
    {
        ParkingLogId = log.Id;
        VehicleId = log.VehicleId;
        GuardId = log.GuardId;
        EntryTime = log.EntryTime;
        ExitTime = log.ExitTime ?? DateTime.UtcNow;
    }
}