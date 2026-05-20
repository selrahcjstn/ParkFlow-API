namespace ParkFlow.Application.Features.ParkingLogs.DTOs;
public record ParkingLogHistoryDto(
    string FirstName,
    string LastName,
    string PhoneNumber,
    string Status,
    string PlateNumber,
    string Brand,
    string VehicleType,
    string EntryTime,
    string? ExitTime,
    string SettlementStatus,
    decimal TotalFee,
    string MustExitBy,
    string TotalParkingHours
    );
