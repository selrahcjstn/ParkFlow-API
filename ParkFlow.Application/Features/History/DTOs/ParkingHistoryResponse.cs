using System;

namespace ParkFlow.Application.Features.History.DTOs;

public class ParkingHistoryResponse
{
    // Session Information
    public Guid SessionId { get; set; }
    public Guid Id => SessionId;
    public string EntryMethod { get; set; } = "QrCode";

    // Owner Information
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string? Email { get; set; }
    public string RoleName { get; set; } = null!;

    // Vehicle Information
    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Type { get; set; } = null!;

    // Session Information
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public double? ParkingDuration { get; set; }
    public double? TotalParkingHours { get; set; }
    public bool HasViolation { get; set; }
    public decimal ViolationFee { get; set; }
    public decimal? PenaltyFee { get; set; }
    public decimal? Amount { get; set; }
    public double OverstayHours { get; set; }
    public bool IsPaid { get; set; }
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = "Completed";
}
