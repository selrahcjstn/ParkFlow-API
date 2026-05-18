namespace ParkFlow.Domain.Entities;

public class Violation : BaseEntity
{
    public Guid LogId { get; private set; }
    public ParkingLog ParkingLog { get; private set; } = null!;

    public ViolationType ViolationType { get; private set; }

    public decimal PenaltyFee { get; private set; }

    public SettlementStatus SettlementStatus { get; private set; }
    private Violation() { }

    public Violation(Guid logId, ViolationType violationType, decimal penaltyFee)
    {
        LogId = logId;
        ViolationType = violationType;
        PenaltyFee = penaltyFee;
        SettlementStatus = SettlementStatus.Pending;
    }
}