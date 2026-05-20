using System.Security.Cryptography.X509Certificates;

namespace ParkFlow.Application.Features.ParkingLogs.DTOs
{
    public record GetActiveParkingSessionResponse(
    // Owner Details
    string FirstName,
    string LastName,
    string PhoneNumber,

    // Vehicle Info
    string Status,
    string PlateNumber,
    string Brand,

    // Session Details
    DateTime EntryTime,
    DateTime MaximumExitTime,
    string TotalParkingHours);
}
