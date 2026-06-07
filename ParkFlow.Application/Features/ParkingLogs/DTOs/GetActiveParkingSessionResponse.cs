namespace ParkFlow.Application.Features.ParkingLogs.DTOs
{
    public record GetActiveParkingSessionResponse(
    // Owner Details
    string FirstName,
    string LastName,
    string? MiddleName,
    string PhoneNumber,

    // Vehicle Info
    string Status,
    string PlateNumber,
    string Brand,
    string VehicleType,

    // Session Details
    DateTime EntryTime,
    DateTime MaximumExitTime,
    double OverstayHours,
    decimal Amount,
    string TotalParkingHours,
    string EntryMethod);
}
