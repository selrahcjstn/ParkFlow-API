namespace ParkFlow.Domain.Entities;

public class Violation : BaseEntity
{
    public Guid ParkingLogId { get; private set; }
    public ParkingLog ParkingLog { get; private set; } = null!;

    public ViolationType ViolationType { get; private set; }

    public decimal PenaltyFee { get; private set; }

    public SettlementStatus SettlementStatus { get; private set; }

    private Violation() { }

    public Violation(
        Guid parkingLogId,
        decimal penaltyFee
)
    {
        ParkingLogId = parkingLogId;
        ViolationType = ViolationType.Overstay;
        PenaltyFee = penaltyFee;
        SettlementStatus = SettlementStatus.Pending;
    }
}
