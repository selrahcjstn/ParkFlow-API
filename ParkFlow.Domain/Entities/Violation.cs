namespace ParkFlow.Domain.Entities;

public class Violation : BaseEntity
{
    public Guid ParkingLogId { get; private set; }
    public ParkingLog ParkingLog { get; private set; } = null!;

    public ViolationType ViolationType { get; private set; }

    public decimal PenaltyFee { get; private set; }

    public SettlementStatus SettlementStatus { get; private set; }

    public ParkingStatus RecordedStatus { get; private set; }

    public DateTime RecordedEntryTime { get; private set; }

    public DateTime RecordedExitTime { get; private set; }

    public int RecordedOverstayMinutes { get; private set; }
    private Violation() { }

    public Violation(
        Guid parkingLogId,
        ViolationType violationType,
        decimal penaltyFee,
        ParkingStatus recordedStatus,
        DateTime recordedEntryTime,
        DateTime recordedExitTime,
        int recordedOverstayMinutes)
    {
        ParkingLogId = parkingLogId;
        ViolationType = violationType;
        PenaltyFee = penaltyFee;
        SettlementStatus = SettlementStatus.Pending;
        RecordedStatus = recordedStatus;
        RecordedEntryTime = recordedEntryTime;
        RecordedExitTime = recordedExitTime;
        RecordedOverstayMinutes = recordedOverstayMinutes;
    }
}
