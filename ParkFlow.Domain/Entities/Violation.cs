using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class Violation : BaseEntity
{
    public Guid ParkingLogId { get; private set; }
    public ParkingLog ParkingLog { get; private set; } = null!;

    public string ReferenceNumber { get; private set; } = null!;

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
        ReferenceNumber = $"VIO-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public Violation(
        Guid parkingLogId,
        decimal penaltyFee,
        ViolationType violationType
    )
    {
        ParkingLogId = parkingLogId;
        ViolationType = violationType;
        PenaltyFee = penaltyFee;
        SettlementStatus = SettlementStatus.Pending;
        var prefix = (violationType == ViolationType.ReservationCharge || violationType == ViolationType.ManualParkingCharge) ? "CHG" : "VIO";
        ReferenceNumber = $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public void MarkAsPaid()
    {
        SettlementStatus = SettlementStatus.Settled;
        UpdatedAt = DateTime.UtcNow;
    }
}
