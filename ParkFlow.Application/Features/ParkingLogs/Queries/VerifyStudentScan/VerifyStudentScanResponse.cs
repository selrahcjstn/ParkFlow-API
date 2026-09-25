namespace ParkFlow.Application.Features.ParkingLogs.Queries.VerifyStudentScan;

public class VerifyStudentScanResponse
{
    public bool IsValid { get; set; }
    public string EntryStatus { get; set; } = string.Empty; // "Approved", "OutOfSchedule", "NoScheduleToday", "CorNotVerified", "NotFound"
    public string StatusMessage { get; set; } = string.Empty;

    // Student Information
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Program { get; set; } = string.Empty;
    public string? Section { get; set; }
    public int? YearLevel { get; set; }
    public string? ProfilePictureUrl { get; set; }

    // Schedule Information
    public string TodaySchedule { get; set; } = string.Empty;
    public string? AllowedEntryWindow { get; set; }
    public string CorStatus { get; set; } = string.Empty;

    // Vehicle Information
    public bool HasRegisteredVehicle { get; set; }
    public Guid? VehicleId { get; set; }
    public string? PlateNumber { get; set; }
    public string? VehicleBrand { get; set; }
    public string? VehicleType { get; set; }
    public string? VehicleQrCodeHash { get; set; }
    public bool IsCurrentlyParked { get; set; }
    public bool HasActiveViolation { get; set; }
    public string? ViolationNotice { get; set; }
}
