using System;

namespace ParkFlow.Application.Features.History.DTOs;

public class ParkingHistoryResponse
{
    // Owner Information
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string RoleName { get; set; } = null!;

    // Vehicle Information
    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string Type { get; set; } = null!;

    // Session Information
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public double ParkingDuration { get; set; }
    public bool HasViolation { get; set; }
}
