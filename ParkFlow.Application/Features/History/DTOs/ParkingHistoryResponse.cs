using System;

namespace ParkFlow.Application.Features.History.DTOs;

public class ParkingHistoryResponse
{
    // Owner Information
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string RoleName { get; set; } = null!;

    // Vehicle Information
    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string? Color { get; set; }
    public string? MotorPictureUrl { get; set; }
    public string? OrcrDocumentUrl { get; set; }

    // Session Information
    public DateTime EntryTime { get; set; }
    public DateTime? ExitTime { get; set; }
    public double? ParkingDuration { get; set; }
    public bool HasViolation { get; set; }
    public decimal ViolationFee { get; set; }
    public double OverstayHours { get; set; }
    public bool IsPaid { get; set; }
    public string? ReferenceNumber { get; set; }
}
