namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public class CreateParkingLogResponse
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? MiddleName { get; set; }
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;
    public string QrCodeHash { get; set; } = null!;
    public string VehicleType { get; set; } = null!;
    public DateTime? EntryTime { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? MaximumExitTime { get; set; }
}
