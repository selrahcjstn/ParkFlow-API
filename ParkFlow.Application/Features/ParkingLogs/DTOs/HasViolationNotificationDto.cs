using System;

namespace ParkFlow.Application.Features.ParkingLogs.DTOs
{
    public class HasViolationNotificationDto
    {
        public string ReferenceNumber { get; set; } = null!;
        public string RefNumber { get; set; } = null!;
        public DateTime IssuedDate { get; set; }
        public DateTime IssuedTime { get; set; }
        public string IssuedBy { get; set; } = null!;
        public double OverstayHours { get; set; }
        public string PlateNumber { get; set; } = null!;
        public decimal Amount { get; set; }
        public string ViolationType { get; set; } = null!;
        public bool IsViolation { get; set; }
    }
}
