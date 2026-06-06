using System;

namespace ParkFlow.Application.Features.Violations.DTOs;

public class ViolationPaymentReceiptDto
{
    public string ReferenceNumber { get; set; } = null!;
    public string ViolationType { get; set; } = null!;
    public decimal PenaltyFee { get; set; }
    public string SettlementStatus { get; set; } = null!;
    public DateTime PaidAt { get; set; }

    // Owner Info
    public string OwnerFirstName { get; set; } = null!;
    public string OwnerLastName { get; set; } = null!;
    public string? OwnerMiddleName { get; set; }

    // Vehicle Info
    public string PlateNumber { get; set; } = null!;
    public string VehicleBrand { get; set; } = null!;
    public string VehicleType { get; set; } = null!;

    // Processor Info
    public string GuardName { get; set; } = null!;
}
