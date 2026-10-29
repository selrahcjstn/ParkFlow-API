using ParkFlow.Domain.Enums;

namespace ParkFlow.Domain.Entities;

public class Violation : BaseEntity
{
    public Guid? ParkingLogId { get; private set; }
    public ParkingLog ParkingLog { get; private set; } = null!;

    public Guid? VisitSessionId { get; private set; }
    public VisitSession? VisitSession { get; private set; }

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
        PenaltyFee = penaltyFee >= 0m ? penaltyFee : throw new ArgumentOutOfRangeException(nameof(penaltyFee));
        SettlementStatus = penaltyFee == 0m ? SettlementStatus.Settled : SettlementStatus.Pending;
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
        PenaltyFee = penaltyFee >= 0m ? penaltyFee : throw new ArgumentOutOfRangeException(nameof(penaltyFee));
        SettlementStatus = penaltyFee == 0m ? SettlementStatus.Settled : SettlementStatus.Pending;
        var prefix = (violationType == ViolationType.ReservationCharge || violationType == ViolationType.ManualParkingCharge) ? "CHG" : "VIO";
        ReferenceNumber = $"{prefix}-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpper()}";
    }

    public void MarkAsPaid()
    {
        SettlementStatus = SettlementStatus.Settled;
        UpdatedAt = DateTime.UtcNow;
    }

    public static Violation CreateVisitorCharge(VisitSession session, decimal parkingFee = VisitSession.ParkingFee)
    {
        return new Violation(session.Id, parkingFee, ViolationType.ManualParkingCharge)
        {
            ParkingLogId = null,
            VisitSessionId = session.Id,
            VisitSession = session
        };
    }
}
