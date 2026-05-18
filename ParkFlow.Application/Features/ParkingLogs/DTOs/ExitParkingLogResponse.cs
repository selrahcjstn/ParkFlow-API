namespace ParkFlow.Application.Features.ParkingLogs.DTOs;

public class ExitParkingLogResponse
{
    public string PlateNumber { get; set; } = null!;
    public DateTime ExitTime { get; set; }
    public bool IsViolation { get; set; }
    public decimal PenaltyFee { get; set; }
    public Guid? ViolationId { get; set; }
}
