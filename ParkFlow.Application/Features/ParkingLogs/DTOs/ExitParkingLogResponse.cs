namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public class ExitParkingLogResponse
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string VehicleType { get; set; } = null!;
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public double OverstayTime { get; set; }
    public decimal PenaltyFee { get; set; }
}
