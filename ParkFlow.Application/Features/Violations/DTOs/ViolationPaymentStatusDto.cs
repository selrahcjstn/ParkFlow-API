namespace ParkFlow.Application.Features.Violations.DTOs;

public class ViolationPaymentStatusDto
{
    public string ReferenceNumber { get; set; } = null!;
    public string ViolationType { get; set; } = null!;
    public decimal PenaltyFee { get; set; }
    public string SettlementStatus { get; set; } = null!;
    public bool IsPaid { get; set; }
    public DateTime IssuedAt { get; set; }
}
