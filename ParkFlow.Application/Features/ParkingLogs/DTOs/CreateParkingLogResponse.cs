namespace ParkFlow.Application.Features.ParkingLogs.DTOs;
public class CreateParkingLogResponse
{
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Role { get; set; } = null!;
    public string Status { get; set; } = null!;

    public string IdNumber { get; set; } = null!;
    public string? Course { get; set; }
    public int? YearLevel { get; set; }
    public string? Section { get; set; }
    public string? Department { get; set; }

    public string PlateNumber { get; set; } = null!;
    public string Brand { get; set; } = null!;

    public DateTime EntryTime { get; set; }
    public DateTime EntryDate { get; set; }
    public Guid LogId { get; set; }
}