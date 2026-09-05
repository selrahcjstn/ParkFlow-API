using ParkFlow.Domain.Enums;

namespace ParkFlow.Application.Features.Reservations.Queries.VerifyReservationScan;

public class VerifyReservationScanResponse
{
    public bool IsValid { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public Guid ReservationId { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public ReservationType Type { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public ReservationStatus Status { get; set; }
    public DateTime ReservationDate { get; set; }
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string DriverEmail { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string VehicleQrCodeHash { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
