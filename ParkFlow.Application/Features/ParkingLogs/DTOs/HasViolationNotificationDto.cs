namespace ParkFlow.Application.Features.ParkingLogs.DTOs
{
    public class HasViolationNotificationDto
    {
        public string FullName { get; set; } = null!;

        // Session Details
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public DateTime EndTime { get; set; }
        public double OverstayTime { get; set; }
        public decimal PenaltyFee { get; set; }
        public bool IsViolation { get; set; }
        public Guid? ViolationId { get; set; }
        public string? ViolationType { get; set; }
    }
}
