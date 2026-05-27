namespace ParkFlow.Application.Features.Violations.DTOs;

public class ViolationHistoryResponse
{
    // Owner Information
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string RoleName { get; set; } = null!;

    // Vehicle Information
    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string VehicleType { get; set; } = null!;

    // Violation Information
    public Guid ViolationId { get; set; }
    public string ReferenceNumber { get; set; } = null!;
    public string ViolationType { get; set; } = null!;
    public decimal PenaltyFee { get; set; }
    public string SettlementStatus { get; set; } = null!;
    public bool IsPaid { get; set; }

    // Session Information
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public DateTime IssuedAt { get; set; }
}
